using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class MobileAuthTests(NmsApiFixture api)
{
    private async Task<HttpResponseMessage> SendAsync(string path, object body, bool mobile)
    {
        using var http = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (mobile) request.Headers.Add("X-NMS-Client", "mobile");
        return await http.SendAsync(request);
    }

    [Fact]
    public async Task The_phone_app_gets_a_refresh_token_it_can_send_back_and_the_website_never_does()
    {
        var user = await api.CreateUserAsync("teacher");
        var login = new { tenant = api.TenantSlug, email = user.Email, password = user.Password };

        var web = await SendAsync("/api/v1/auth/login", login, mobile: false);
        var phone = await SendAsync("/api/v1/auth/login", login, mobile: true);
        var webBody = await web.Content.ReadAsStringAsync();
        var phoneJson = System.Text.Json.JsonDocument.Parse(await phone.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var token = phoneJson.GetProperty("refresh_token").GetString()!;

        var refreshed = await SendAsync("/api/v1/auth/refresh", new { refresh_token = token }, mobile: true);
        var notPhone = await SendAsync("/api/v1/auth/refresh", new { refresh_token = token }, mobile: false);

        web.StatusCode.Should().Be(HttpStatusCode.OK);
        webBody.Should().NotContain("refresh_token");
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await refreshed.Content.ReadAsStringAsync()).Should().Contain("refresh_token");
        notPhone.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_emergency_data_for_offline_use_is_for_attendance_staff_only_and_is_written_to_the_change_history()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var parent = await api.CreateUserAsync("parent");
        var room = await api.CreateRoomAsync();
        var child = await api.CreateStudentAsync(room, $"fam-{Guid.NewGuid():N}@test.local", medical: "Asthma, inhaler in bag");
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });

        var data = await api.Admin.GetAsync("/api/v1/emergency/data");
        var asParent = await parent.Client.GetAsync("/api/v1/emergency/data");
        var asTeacher = await teacher.Client.GetAsync("/api/v1/emergency/data");
        var audit = await api.Admin.GetAsync("/api/v1/audit-logs?action=export&limit=5");

        data.Status.Should().Be(HttpStatusCode.OK);
        data.Data.GetProperty("children").EnumerateArray().Should().Contain(c => c.GetProperty("medical").GetString() == "Asthma, inhaler in bag");
        asParent.Status.Should().Be(HttpStatusCode.Forbidden);
        asTeacher.Status.Should().Be(HttpStatusCode.OK);
        audit.Items.Should().NotBeEmpty();
    }
}
