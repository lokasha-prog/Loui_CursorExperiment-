using System.Net;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TenantIsolationAndAuditTests(NmsApiFixture api)
{
    private async Task<(Guid StudentId, Guid TenantId)> CreateSecondCentreWithStudentAsync(string firstName)
    {
        var otherTenant = new Tenant { Name = new Dictionary<string, string> { ["en"] = "Other Centre" }, Slug = $"other-{Guid.NewGuid():N}"[..20] };
        await api.WithTenantAsync(Guid.Empty, async db =>
        {
            db.Tenants.Add(otherTenant);
            return await db.SaveChangesAsync();
        });
        var studentId = await api.WithTenantAsync(otherTenant.Id, async db =>
        {
            var student = new Student { FirstName = firstName, LastName = "Other", DateOfBirth = new DateOnly(2023, 1, 1), Barcode = $"STU{Random.Shared.NextInt64(100000000, 999999999)}" };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            return student.Id;
        });
        return (studentId, otherTenant.Id);
    }

    private async Task<T> RawScalarAsync<T>(Guid? tenantId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(api.DatabaseConnection);
        await connection.OpenAsync();
        if (tenantId is not null)
        {
            await using var context = connection.CreateCommand();
            context.CommandText = "EXEC sp_set_session_context @key = N'tenant_id', @value = @t;";
            context.Parameters.AddWithValue("@t", tenantId.Value);
            await context.ExecuteNonQueryAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    [Fact]
    public async Task Another_centres_students_never_appear_through_the_api()
    {
        var (foreign, _) = await CreateSecondCentreWithStudentAsync("Foreign");
        await api.CreateStudentAsync();

        var list = await api.Admin.GetAsync("/api/v1/students?limit=100&search=Foreign");
        var direct = await api.Admin.GetAsync($"/api/v1/students/{foreign}");
        var update = await api.Admin.PatchAsync($"/api/v1/students/{foreign}", new { student = new { first_name = "Hacked", last_name = "X", date_of_birth = "2023-01-01" } });
        var delete = await api.Admin.DeleteAsync($"/api/v1/students/{foreign}");

        list.Items.Should().BeEmpty();
        direct.Status.Should().Be(HttpStatusCode.NotFound);
        update.Status.Should().Be(HttpStatusCode.NotFound);
        delete.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Database_row_level_security_blocks_cross_centre_reads_even_without_ef_filters()
    {
        var (foreign, otherTenantId) = await CreateSecondCentreWithStudentAsync("RlsTarget");

        var asOtherCentre = await RawScalarAsync<int>(otherTenantId, "SELECT COUNT(*) FROM Students WHERE Id = @id", ("@id", foreign));
        var asOurCentre = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM Students WHERE Id = @id", ("@id", foreign));
        var withNoCentre = await RawScalarAsync<int>(null, "SELECT COUNT(*) FROM Students", Array.Empty<(string, object)>());
        var viaEfIgnoringFilters = await api.WithTenantAsync(api.TenantId, db =>
            db.Students.IgnoreQueryFilters().CountAsync(s => s.Id == foreign));

        asOtherCentre.Should().Be(1);
        asOurCentre.Should().Be(0);
        withNoCentre.Should().Be(0, "a connection with no tenant sees nothing");
        viaEfIgnoringFilters.Should().Be(0, "the database refuses even when the application forgets its filter");
    }

    [Fact]
    public async Task Database_refuses_writing_rows_into_another_centre()
    {
        var act = () => RawScalarAsync<int>(
            api.TenantId,
            "INSERT INTO Labels (Id, TenantId, Name, Color, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES (NEWID(), @other, N'{}', N'#000000', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), NEWID(), NEWID(), 0)",
            ("@other", Guid.NewGuid()));

        await act.Should().ThrowAsync<SqlException>();
    }

    [Fact]
    public async Task Every_change_is_written_to_the_audit_log_with_who_and_what()
    {
        var me = (await api.Admin.GetAsync("/api/v1/auth/me")).Data.GetProperty("id").GetGuid();
        var id = await api.CreateStudentAsync(first: "Audited");
        await api.Admin.PatchAsync($"/api/v1/students/{id}", new { student = new { first_name = "Renamed", last_name = "Audit", date_of_birth = "2023-04-12" } });
        await api.Admin.DeleteAsync($"/api/v1/students/{id}");

        var creates = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM AuditLogs WHERE EntityId = @id AND Action = 'create' AND EntityType = 'Student' AND UserId = @u", ("@id", id), ("@u", me));
        var updates = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM AuditLogs WHERE EntityId = @id AND Action = 'update'", ("@id", id));
        var deletes = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM AuditLogs WHERE EntityId = @id AND Action = 'delete'", ("@id", id));
        var diff = await RawScalarAsync<string>(api.TenantId, "SELECT TOP 1 ChangedFields FROM AuditLogs WHERE EntityId = @id AND Action = 'update' ORDER BY OccurredAt", ("@id", id));

        creates.Should().Be(1);
        updates.Should().BeGreaterThanOrEqualTo(1);
        deletes.Should().Be(1);
        diff.Should().Contain("Audited").And.Contain("Renamed");
    }

    [Fact]
    public async Task Sensitive_values_are_masked_in_the_audit_log_and_encrypted_in_the_database()
    {
        const string secret = "Severe peanut allergy - carries epipen";
        var id = await api.CreateStudentAsync(medical: secret);

        var audit = await RawScalarAsync<string>(api.TenantId, "SELECT ChangedFields FROM AuditLogs WHERE EntityId = @id AND Action = 'create'", ("@id", id));
        var stored = await RawScalarAsync<string>(api.TenantId, "SELECT MedicalNotes FROM Students WHERE Id = @id", ("@id", id));
        var viaApi = await api.Admin.GetAsync($"/api/v1/students/{id}");

        audit.Should().NotContain("peanut").And.Contain("[encrypted]");
        stored.Should().NotContain("peanut").And.NotBeNullOrEmpty();
        viaApi.Data.GetProperty("medical_notes").GetString().Should().Be(secret);
    }

    [Fact]
    public async Task Audit_log_rows_cannot_be_changed_or_removed()
    {
        await api.CreateStudentAsync();

        var update = () => RawScalarAsync<int>(api.TenantId, "UPDATE AuditLogs SET Action = 'delete'", Array.Empty<(string, object)>());
        var delete = () => RawScalarAsync<int>(api.TenantId, "DELETE FROM AuditLogs", Array.Empty<(string, object)>());

        (await update.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("append-only");
        (await delete.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("append-only");
    }

    [Fact]
    public async Task Sign_ins_and_failed_sign_ins_are_audited()
    {
        var user = await api.CreateUserAsync("teacher");
        await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = user.Email, password = "wrong-password" });
        await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = $"ghost-{Guid.NewGuid():N}@test.local", password = "x" });

        var logins = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM AuditLogs WHERE Action = 'login' AND UserId = @u", ("@u", user.UserId));
        var anonymousAttempts = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM AuditLogs WHERE Action = 'login' AND UserId IS NULL");

        logins.Should().BeGreaterThanOrEqualTo(1);
        anonymousAttempts.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Created_rows_record_who_made_them_and_when()
    {
        var me = (await api.Admin.GetAsync("/api/v1/auth/me")).Data.GetProperty("id").GetGuid();
        var id = await api.CreateStudentAsync();

        var createdBy = await RawScalarAsync<Guid>(api.TenantId, "SELECT CreatedBy FROM Students WHERE Id = @id", ("@id", id));
        var tenant = await RawScalarAsync<Guid>(api.TenantId, "SELECT TenantId FROM Students WHERE Id = @id", ("@id", id));
        var createdAt = await RawScalarAsync<DateTimeOffset>(api.TenantId, "SELECT CreatedAt FROM Students WHERE Id = @id", ("@id", id));

        createdBy.Should().Be(me);
        tenant.Should().Be(api.TenantId);
        createdAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task Soft_deleted_rows_stay_in_the_database_for_the_record()
    {
        var id = await api.CreateStudentAsync();
        await api.Admin.DeleteAsync($"/api/v1/students/{id}");

        var rows = await RawScalarAsync<int>(api.TenantId, "SELECT COUNT(*) FROM Students WHERE Id = @id AND IsDeleted = 1 AND DeletedAt IS NOT NULL", ("@id", id));

        rows.Should().Be(1);
    }
}
