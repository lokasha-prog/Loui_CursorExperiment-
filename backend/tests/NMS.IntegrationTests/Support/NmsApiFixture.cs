using Microsoft.AspNetCore.Hosting;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Auth;
using NMS.Domain.Identity;
using NMS.Infrastructure.Persistence;
using Xunit;

namespace NMS.IntegrationTests.Support;

/// <summary>
/// Starts the real API against a brand-new, uniquely named database on the local SQL Server and deletes it afterwards.
/// No data is seeded: the first administrator and everything else is created through the API by the tests themselves.
/// The server is chosen by the NMS_TEST_CONNECTION_STRING environment variable (a connection string without a database).
/// </summary>
public sealed class NmsApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _serverConnection =
        Environment.GetEnvironmentVariable("NMS_TEST_CONNECTION_STRING")
        ?? throw new InvalidOperationException("Set NMS_TEST_CONNECTION_STRING to a SQL Server connection string without a database name.");

    private readonly string _database = $"NMS_Test_{Guid.NewGuid():N}";

    public const int GraceSeconds = 2;

    public FakeAiBrain Ai { get; } = new();

    public string BootstrapToken { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public string AdminEmail { get; } = $"admin-{Guid.NewGuid():N}@test.local";

    public string AdminPassword { get; } = NewPassword();

    public string TenantSlug { get; } = $"centre-{Guid.NewGuid():N}"[..20];

    public Guid TenantId { get; private set; }

    public ApiClient Admin { get; private set; } = null!;

    public string DatabaseConnection => new SqlConnectionStringBuilder(_serverConnection) { InitialCatalog = _database }.ConnectionString;

    public static string NewPassword() => $"Aa1-{Convert.ToHexString(RandomNumberGenerator.GetBytes(10))}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Nms", DatabaseConnection);
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Encryption:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Auth:BootstrapToken", BootstrapToken);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseSetting("DailyReports:GenerateAt", "00:00");
        builder.UseSetting("RateLimiting:AuthenticatedPerMinute", "100000");
        builder.UseSetting("RateLimiting:AnonymousPerMinute", "100000");
        builder.UseSetting("RateLimiting:LoginPerMinute", "100000");
        builder.UseSetting("RateLimiting:SessionPerMinute", "100000");
        builder.UseSetting("Auth:RefreshReuseGraceSeconds", GraceSeconds.ToString());
        // Tests never call the real AI provider.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NMS.Application.Ai.IAiBrain>();
            services.AddSingleton(Ai);
            services.AddSingleton<NMS.Application.Ai.IAiBrain>(Ai);
        });
    }

    public async Task InitializeAsync()
    {
        var anonymous = NewClient();
        var response = await anonymous.PostAsync("/api/v1/auth/bootstrap-super-admin", new
        {
            bootstrap_token = BootstrapToken,
            tenant_slug = TenantSlug,
            tenant_name = new Dictionary<string, string> { ["en"] = "Test Nursery" },
            admin_email = AdminEmail,
            admin_first_name = "Test",
            admin_last_name = "Admin",
            admin_password = AdminPassword,
        });
        response.Status.Should().Be(HttpStatusCode.Created, response.Raw);
        TenantId = response.Data.GetProperty("tenant").GetProperty("id").GetGuid();
        Admin = await LoginAsync(AdminEmail, AdminPassword);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await using var connection = new SqlConnection(_serverConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(N'{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END";
        await command.ExecuteNonQueryAsync();
    }

    public ApiClient NewClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    public async Task<ApiClient> LoginAsync(string email, string password, string? tenant = null)
    {
        var client = NewClient();
        var response = await client.PostAsync("/api/v1/auth/login", new { tenant, email, password });
        response.Status.Should().Be(HttpStatusCode.OK, response.Raw);
        client.SetBearer(response.Data.GetProperty("access_token").GetString()!);
        client.RefreshCookie = response.RefreshCookie;
        return client;
    }

    /// <summary>Creates a user through the API with the given roles and returns a client already signed in as them.</summary>
    public async Task<(ApiClient Client, Guid UserId, string Email, string Password)> CreateUserAsync(
        string role, Guid? staffId = null, Guid? guardianId = null)
    {
        var email = $"{role}-{Guid.NewGuid():N}@test.local";
        var password = NewPassword();
        var created = await Admin.PostAsync("/api/v1/users", new
        {
            email,
            first_name = "Test",
            last_name = role,
            password,
            roles = new[] { role },
            staff_id = staffId,
            guardian_id = guardianId,
        });
        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        return (await LoginAsync(email, password), created.Data.GetProperty("id").GetGuid(), email, password);
    }

    public async Task<Guid> CreateRoomAsync(string? name = null, int capacity = 12, int ratio = 4)
    {
        var response = await Admin.PostAsync("/api/v1/rooms", new
        {
            name = new Dictionary<string, string> { ["en"] = name ?? $"Room {Guid.NewGuid():N}" },
            capacity,
            min_age_months = 12,
            max_age_months = 36,
            ratio_staff_to_student = ratio,
        });
        response.Status.Should().Be(HttpStatusCode.Created, response.Raw);
        return response.Data.GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateStudentAsync(
        Guid? roomId = null, string? guardianEmail = null, string first = "Layla", string? medical = null)
    {
        var response = await Admin.PostAsync("/api/v1/students", new
        {
            student = new
            {
                first_name = first,
                last_name = $"Family{Guid.NewGuid():N}"[..14],
                date_of_birth = "2023-04-12",
                gender = "female",
                room_id = roomId,
                medical_notes = medical,
            },
            guardians = guardianEmail is null
                ? Array.Empty<object>()
                : [new { first_name = "Parent", last_name = "One", email = guardianEmail, relationship = "mother", is_primary = true }],
        });
        response.Status.Should().Be(HttpStatusCode.Created, response.Raw);
        return response.Data.GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateStaffAsync(string? email = null)
    {
        var response = await Admin.PostAsync("/api/v1/staff", new
        {
            staff = new
            {
                first_name = "Noor",
                last_name = "Teacher",
                email = email ?? $"staff-{Guid.NewGuid():N}@test.local",
                hire_date = "2025-09-01",
                status = "active",
                job_title = "Teacher",
                salary_amount_fils = 650000,
            },
        });
        response.Status.Should().Be(HttpStatusCode.Created, response.Raw);
        return response.Data.GetProperty("id").GetGuid();
    }

    public async Task AssignStaffToRoomAsync(Guid staffId, Guid roomId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await Admin.PostAsync($"/api/v1/staff/{staffId}/room-assignments", new
        {
            room_id = roomId,
            day_of_week = 1,
            start_time = "07:30:00",
            end_time = "15:30:00",
            effective_from = today.AddDays(-1).ToString("yyyy-MM-dd"),
        });
        response.Status.Should().Be(HttpStatusCode.Created, response.Raw);
    }

    /// <summary>Gives one e-mail address an account (same password) in this centre and in a brand-new second centre.</summary>
    public async Task<(Tenant First, Tenant Second)> CreateTwoCentresWithSharedUserAsync(string email, string password)
    {
        var hash = Services.GetRequiredService<IPasswordService>().Hash(password);
        var first = await WithTenantAsync(Guid.Empty, db => db.Tenants.FirstAsync(t => t.Id == TenantId));
        var second = new Tenant { Name = new Dictionary<string, string> { ["en"] = "Second Centre" }, Slug = $"second-{Guid.NewGuid():N}"[..20], CreatedAt = DateTimeOffset.UtcNow };
        await WithTenantAsync(Guid.Empty, async db =>
        {
            db.Tenants.Add(second);
            await db.SaveChangesAsync();
            return 0;
        });

        foreach (var centre in new[] { first, second })
        {
            await WithTenantAsync(centre.Id, async db =>
            {
                var user = new AppUser { Email = email, FirstName = "Shared", LastName = "Person", PasswordHash = hash };
                db.AppUsers.Add(user);
                db.LoginDirectory.Add(new LoginDirectoryEntry { Email = email, CentreId = centre.Id, UserId = user.Id });
                await db.SaveChangesAsync();
                return 0;
            });
        }

        return (first, second);
    }

    /// <summary>Runs code in a scope that behaves like the given tenant (used to prove isolation).</summary>
    public async Task<T> WithTenantAsync<T>(Guid tenantId, Func<NmsDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        return await action(scope.ServiceProvider.GetRequiredService<NmsDbContext>());
    }
}

public sealed record ApiReply(HttpStatusCode Status, string Raw, HttpResponseMessage Message)
{
    public JsonElement Root => JsonDocument.Parse(Raw).RootElement;

    public JsonElement Data => Root.GetProperty("data");

    public JsonElement Meta => Root.GetProperty("meta");

    public IEnumerable<JsonElement> Items => Data.EnumerateArray();

    public string? ErrorCode => Root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0
        ? e[0].GetProperty("code").GetString()
        : null;

    public string? RefreshCookie => Message.Headers.TryGetValues("Set-Cookie", out var values)
        ? values.Select(v => v.Split(';')[0]).FirstOrDefault(v => v.StartsWith("nms_refresh="))?["nms_refresh=".Length..]
        : null;

    public string? SetCookieRaw => Message.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join("|", values) : null;
}

public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public string? RefreshCookie { get; set; }

    public string? Bearer { get; private set; }

    public void SetBearer(string token)
    {
        Bearer = token;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public Task<ApiReply> GetAsync(string url) => SendAsync(HttpMethod.Get, url, null);

    public Task<ApiReply> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body ?? new { });

    public Task<ApiReply> PatchAsync(string url, object body) => SendAsync(HttpMethod.Patch, url, body);

    public Task<ApiReply> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, null);

    public async Task<ApiReply> SendAsync(
        HttpMethod method, string url, object? body, string? cookie = null, bool csrfHeader = false)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"nms_refresh={cookie}");
        }

        if (csrfHeader)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        var response = await http.SendAsync(request);
        return new ApiReply(response.StatusCode, await response.Content.ReadAsStringAsync(), response);
    }

    public async Task<ApiReply> PostFileAsync(string url, byte[] bytes, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(part, "file", fileName);
        var response = await http.PostAsync(url, content);
        return new ApiReply(response.StatusCode, await response.Content.ReadAsStringAsync(), response);
    }

    public async Task<(HttpStatusCode Status, byte[] Bytes, string? ContentType)> GetBytesAsync(string url)
    {
        var response = await http.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.MediaType);
    }

    public Task<ApiReply> RefreshAsync(string cookie) =>
        SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", null, cookie, csrfHeader: true);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<NmsApiFixture>
{
    public const string Name = "api";
}

public sealed class FakeAiBrain : NMS.Application.Ai.IAiBrain
{
    public bool IsConfigured { get; set; } = true;

    public string ModelName => "fake-model";

    public string? LastSystem { get; private set; }

    public string? LastUser { get; private set; }

    public NMS.Shared.Results.Result<string> Answer { get; set; } = NMS.Shared.Results.Result<string>.Success("Attendance was steady.");

    public Task<NMS.Shared.Results.Result<string>> CompleteAsync(string system, string user, CancellationToken cancellationToken)
    {
        LastSystem = system;
        LastUser = user;
        return Task.FromResult(Answer);
    }
}
