using System.Net;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NMS.Api.Jobs;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuditRetentionTests(NmsApiFixture api)
{
    private async Task<int> RawAsync(string sql, bool asPurger = false)
    {
        await using var connection = new SqlConnection(api.DatabaseConnection);
        await connection.OpenAsync();
        await using var context = connection.CreateCommand();
        context.CommandText = "EXEC sp_set_session_context @key = N'tenant_id', @value = @t;" + (asPurger ? " EXEC sp_set_session_context @key = N'audit_purge', @value = 1;" : "");
        context.Parameters.AddWithValue("@t", api.TenantId);
        await context.ExecuteNonQueryAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync();
    }

    private async Task OldEntriesAsync(int count, int daysAgo)
    {
        for (var i = 0; i < count; i++)
        {
            await RawAsync($"INSERT INTO AuditLogs (Id, TenantId, EntityType, EntityId, Action, OccurredAt) VALUES (NEWID(), '{api.TenantId}', N'legacy', NEWID(), N'create', DATEADD(DAY, -{daysAgo}, SYSDATETIMEOFFSET()))");
        }
    }

    [Fact]
    public async Task The_policy_has_a_safe_default_a_legal_floor_and_is_for_super_administrators_only()
    {
        var teacher = await api.CreateUserAsync("teacher");

        var current = await api.Admin.GetAsync("/api/v1/audit-logs/retention");
        var tooShort = await api.Admin.SendAsync(HttpMethod.Put, "/api/v1/audit-logs/retention", new { retention_days = 30, auto_purge_enabled = true });
        var saved = await api.Admin.SendAsync(HttpMethod.Put, "/api/v1/audit-logs/retention", new { retention_days = 730, auto_purge_enabled = false });

        current.Data.GetProperty("retention_days").GetInt32().Should().BeGreaterThanOrEqualTo(365);
        current.Data.GetProperty("min_retention_days").GetInt32().Should().Be(365);
        tooShort.Status.Should().Be(HttpStatusCode.BadRequest);
        saved.Data.GetProperty("retention_days").GetInt32().Should().Be(730);
        (await teacher.Client.GetAsync("/api/v1/audit-logs/retention")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.Client.PostAsync("/api/v1/audit-logs/retention/actions/purge", new { confirm = true })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manual_purge_needs_confirmation_removes_only_expired_history_and_is_itself_recorded()
    {
        await api.Admin.SendAsync(HttpMethod.Put, "/api/v1/audit-logs/retention", new { retention_days = 400, auto_purge_enabled = false });
        await OldEntriesAsync(3, 900);
        await OldEntriesAsync(2, 380);

        var preview = await api.Admin.GetAsync("/api/v1/audit-logs/retention");
        var refused = await api.Admin.PostAsync("/api/v1/audit-logs/retention/actions/purge", new { confirm = false });
        var purged = await api.Admin.PostAsync("/api/v1/audit-logs/retention/actions/purge", new { confirm = true });
        var after = await api.Admin.GetAsync("/api/v1/audit-logs/retention");
        var trail = await api.Admin.GetAsync("/api/v1/audit-logs?entity_type=audit_log&action=delete");

        preview.Data.GetProperty("eligible_for_purge").GetInt64().Should().BeGreaterThanOrEqualTo(3);
        refused.Status.Should().Be(HttpStatusCode.BadRequest);
        purged.Status.Should().Be(HttpStatusCode.OK, purged.Raw);
        purged.Data.GetProperty("rows_deleted").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        purged.Data.GetProperty("mode").GetString().Should().Be("manual");
        after.Data.GetProperty("eligible_for_purge").GetInt64().Should().Be(0);
        after.Data.GetProperty("recent_runs").GetArrayLength().Should().BeGreaterThan(0);
        (await api.Admin.GetAsync("/api/v1/audit-logs?entity_type=legacy&limit=100")).Items.Should().HaveCount(2, "entries inside the policy stay");
        trail.Items.Should().NotBeEmpty("the purge is written to the history it trimmed");
    }

    [Fact]
    public async Task The_database_refuses_to_delete_young_history_even_to_the_purge_procedure_and_refuses_everyone_else()
    {
        await OldEntriesAsync(1, 100);

        var asPurgerYoung = () => RawAsync("DELETE FROM AuditLogs WHERE EntityType = N'legacy' AND OccurredAt > DATEADD(DAY, -200, SYSDATETIMEOFFSET())", asPurger: true);
        var ordinary = () => RawAsync("DELETE FROM AuditLogs WHERE EntityType = N'legacy'");
        var update = () => RawAsync("UPDATE AuditLogs SET Action = N'x' WHERE EntityType = N'legacy'", asPurger: true);

        (await asPurgerYoung.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("365 days");
        (await ordinary.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("append-only");
        (await update.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("append-only");
    }

    [Fact]
    public async Task The_automatic_job_purges_once_a_day_only_when_switched_on()
    {
        var jobs = api.Services.GetRequiredService<ScheduledJobsService>();
        await api.Admin.SendAsync(HttpMethod.Put, "/api/v1/audit-logs/retention", new { retention_days = 400, auto_purge_enabled = false });
        await OldEntriesAsync(2, 1000);

        await jobs.RunOnceAsync(CancellationToken.None);
        var off = (await api.Admin.GetAsync("/api/v1/audit-logs/retention")).Data.GetProperty("eligible_for_purge").GetInt64();
        await api.Admin.SendAsync(HttpMethod.Put, "/api/v1/audit-logs/retention", new { retention_days = 400, auto_purge_enabled = true });
        await jobs.RunOnceAsync(CancellationToken.None);
        var on = await api.Admin.GetAsync("/api/v1/audit-logs/retention");
        await OldEntriesAsync(1, 1000);
        await jobs.RunOnceAsync(CancellationToken.None);
        var again = await api.Admin.GetAsync("/api/v1/audit-logs/retention");

        off.Should().BeGreaterThanOrEqualTo(2, "switched off means nothing is removed");
        on.Data.GetProperty("eligible_for_purge").GetInt64().Should().Be(0);
        on.Data.GetProperty("recent_runs")[0].GetProperty("mode").GetString().Should().Be("auto");
        again.Data.GetProperty("eligible_for_purge").GetInt64().Should().BeGreaterThanOrEqualTo(1, "it ran less than a day ago");
    }

    [Fact]
    public async Task A_spreadsheet_copy_can_be_kept_before_purging_and_taking_it_is_recorded()
    {
        var csv = await api.Admin.GetAsync("/api/v1/audit-logs/export");
        var trail = await api.Admin.GetAsync("/api/v1/audit-logs?action=export&entity_type=audit_log");

        csv.Status.Should().Be(HttpStatusCode.OK);
        csv.Raw.TrimStart('﻿').Should().StartWith("When (UTC),User,Action");
        trail.Items.Should().NotBeEmpty();
    }
}
