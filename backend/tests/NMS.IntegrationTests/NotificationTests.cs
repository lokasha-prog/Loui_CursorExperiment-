using System.Net;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NMS.Application.Abstractions;
using NMS.Application.Notifications;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class NotificationTests(NmsApiFixture api)
{
    private async Task<int> RunDispatchAsync()
    {
        using var scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = api.TenantId;
        return (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunNotificationDispatchCommand())).Value;
    }

    private async Task<(Guid Room, string Barcode)> ActiveChildInNewRoomAsync(int ratio)
    {
        var room = await api.CreateRoomAsync(ratio: ratio);
        var child = await api.CreateStudentAsync(room, null);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        return (room, (await api.Admin.GetAsync($"/api/v1/students/{child}")).Data.GetProperty("barcode").GetString()!);
    }

    [Fact]
    public async Task A_staff_shortage_alerts_managers_in_the_app_and_by_email_but_not_teachers()
    {
        var manager = await api.CreateUserAsync("centre_admin");
        var teacher = await api.CreateUserAsync("teacher");
        var before = (await manager.Client.GetAsync("/api/v1/notifications/unread-count")).Data.GetProperty("count").GetInt32();
        var (_, barcode) = await ActiveChildInNewRoomAsync(1);

        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
        var list = await manager.Client.GetAsync("/api/v1/notifications?unread_only=true");
        var count = (await manager.Client.GetAsync("/api/v1/notifications/unread-count")).Data.GetProperty("count").GetInt32();
        var teacherList = await teacher.Client.GetAsync("/api/v1/notifications");

        var alert = list.Items.First(n => n.GetProperty("type").GetString() == "ratio_breach");
        alert.GetProperty("tier").GetString().Should().Be("critical");
        alert.GetProperty("title").GetProperty("ar").GetString().Should().NotBeNullOrEmpty();
        alert.GetProperty("link").GetString().Should().Be("/attendance");
        count.Should().Be(before + 1);
        teacherList.Items.Should().BeEmpty();

        api.Email.Sent.Clear();
        await RunDispatchAsync();
        api.Email.Sent.Should().Contain(m => m.To == manager.Email && m.Subject.Contains("needs more staff"));

        var read = await manager.Client.PostAsync($"/api/v1/notifications/{alert.GetProperty("id").GetGuid()}/actions/read");
        read.Status.Should().Be(HttpStatusCode.OK);
        (await manager.Client.GetAsync("/api/v1/notifications/unread-count")).Data.GetProperty("count").GetInt32().Should().Be(count - 1);
    }

    [Fact]
    public async Task Nobody_can_read_or_mark_someone_elses_notifications()
    {
        var manager = await api.CreateUserAsync("centre_admin");
        var other = await api.CreateUserAsync("centre_admin");
        var (_, barcode) = await ActiveChildInNewRoomAsync(1);
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
        var mine = (await manager.Client.GetAsync("/api/v1/notifications")).Items.First().GetProperty("id").GetGuid();

        var stolen = await other.Client.PostAsync($"/api/v1/notifications/{mine}/actions/read");

        stolen.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_failed_email_is_retried_later_and_a_missing_mail_server_is_reported_as_skipped()
    {
        var manager = await api.CreateUserAsync("centre_admin");
        var (_, barcode) = await ActiveChildInNewRoomAsync(1);
        api.Email.Fail = true;
        try
        {
            await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
            await RunDispatchAsync();
            var attempts = await api.WithTenantAsync(api.TenantId, db => db.NotificationDeliveries
                .Where(d => d.Recipient == manager.Email && d.Channel == "email").Select(d => new { d.Status, d.Attempts }).ToListAsync());

            var row = attempts.Should().ContainSingle().Subject;
            row.Status.Should().Be("pending");
            row.Attempts.Should().Be(1);

            api.Email.Configured = false;
            var other = await ActiveChildInNewRoomAsync(1);
            await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = other.Barcode });
            await RunDispatchAsync();
            (await api.WithTenantAsync(api.TenantId, db => db.NotificationDeliveries.CountAsync(d => d.Status == "skipped"))).Should().BeGreaterThan(0);
        }
        finally
        {
            api.Email.Fail = false;
            api.Email.Configured = true;
        }
    }

    [Fact]
    public async Task People_choose_their_channels_but_critical_alerts_always_reach_the_app()
    {
        var manager = await api.CreateUserAsync("centre_admin");

        var defaults = await manager.Client.GetAsync("/api/v1/notifications/preferences");
        var saved = await manager.Client.SendAsync(HttpMethod.Put, "/api/v1/notifications/preferences", new
        {
            preferences = new object[]
            {
                new { tier = "critical", in_app = false, email = false },
                new { tier = "operational", in_app = true, email = true },
            },
        });

        defaults.Items.Single(p => p.GetProperty("tier").GetString() == "informational").GetProperty("email").GetBoolean().Should().BeTrue();
        saved.Status.Should().Be(HttpStatusCode.OK, saved.Raw);
        var critical = saved.Items.Single(p => p.GetProperty("tier").GetString() == "critical");
        critical.GetProperty("in_app").GetBoolean().Should().BeTrue("a critical alert cannot be switched off");
        critical.GetProperty("email").GetBoolean().Should().BeFalse();
        saved.Items.Single(p => p.GetProperty("tier").GetString() == "operational").GetProperty("email").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_new_message_notifies_the_family_and_the_daily_report_is_a_quiet_email()
    {
        var room = await api.CreateRoomAsync();
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(room, email);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);

        await api.Admin.PostAsync("/api/v1/messages/threads", new { subject = "Hello", body = "Welcome", student_id = child });
        var inApp = await parent.Client.GetAsync("/api/v1/notifications");

        inApp.Items.Should().ContainSingle(n => n.GetProperty("type").GetString() == "message_received");

        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{child}")).Data.GetProperty("barcode").GetString()!;
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
        using (var scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = api.TenantId;
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new NMS.Application.Communication.RunDailyReportsCommand());
        }

        api.Email.Sent.Clear();
        await RunDispatchAsync();

        api.Email.Sent.Should().Contain(m => m.To == parent.Email && m.Subject.Contains("daily report"));
        (await parent.Client.GetAsync("/api/v1/notifications")).Items.Should().NotContain(n => n.GetProperty("type").GetString() == "daily_report");
    }
}
