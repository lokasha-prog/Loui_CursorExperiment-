using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RealtimeTests(NmsApiFixture api)
{
    private async Task<(HubConnection Hub, List<JsonElement> Messages)> ConnectAsync(string token, string eventName)
    {
        var messages = new List<JsonElement>();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/centre"), o =>
            {
                o.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        hub.On<JsonElement>(eventName, m => { lock (messages) { messages.Add(m); } });
        await hub.StartAsync();
        return (hub, messages);
    }

    private static async Task<bool> WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40 && !condition(); i++)
        {
            await Task.Delay(100);
        }

        return condition();
    }

    private async Task<string> TokenAsync(string email, string password) =>
        (await api.NewClient().PostAsync("/api/v1/auth/login", new { email, password })).Data.GetProperty("access_token").GetString()!;

    private async Task<string> ActiveBarcodeAsync(Guid room)
    {
        var id = await api.CreateStudentAsync(room, null);
        await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" });
        return (await api.Admin.GetAsync($"/api/v1/students/{id}")).Data.GetProperty("barcode").GetString()!;
    }

    [Fact]
    public async Task Connecting_without_a_valid_token_is_refused()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/centre"), o =>
            {
                o.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var start = () => hub.StartAsync();

        await start.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Staff_screens_hear_about_a_check_in_but_parents_do_not()
    {
        var room = await api.CreateRoomAsync();
        var staffUser = await api.CreateUserAsync("receptionist");
        var email = $"parent-{Guid.NewGuid():N}@test.local";
        var childId = await api.CreateStudentAsync(room, email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{childId}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var staffToken = await TokenAsync(staffUser.Email, staffUser.Password);
        var parentToken = await TokenAsync(parent.Email, parent.Password);
        var (staffHub, staffMessages) = await ConnectAsync(staffToken, "attendance_changed");
        var (parentHub, parentMessages) = await ConnectAsync(parentToken, "attendance_changed");

        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = await ActiveBarcodeAsync(room) });

        (await WaitAsync(() => staffMessages.Count > 0)).Should().BeTrue();
        staffMessages[0].GetProperty("room_id").GetGuid().Should().Be(room);
        await Task.Delay(300);
        parentMessages.Should().BeEmpty();
        await staffHub.DisposeAsync();
        await parentHub.DisposeAsync();
    }

    [Fact]
    public async Task A_short_room_raises_one_alert_and_one_history_entry_and_clears_when_covered()
    {
        var room = await api.CreateRoomAsync(ratio: 2);
        var (hub, alerts) = await ConnectAsync(api.Admin.Bearer!, "ratio_breach");
        var restored = new List<JsonElement>();
        hub.On<JsonElement>("ratio_restored", m => { lock (restored) { restored.Add(m); } });

        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = await ActiveBarcodeAsync(room) });
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = await ActiveBarcodeAsync(room) });
        var staff = await api.CreateStaffAsync();
        var staffCode = (await api.Admin.GetAsync($"/api/v1/staff/{staff}")).Data.GetProperty("barcode").GetString()!;
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = staffCode, room_id = room });

        (await WaitAsync(() => alerts.Count > 0)).Should().BeTrue();
        (await WaitAsync(() => restored.Count > 0)).Should().BeTrue();
        var history = await api.Admin.GetAsync($"/api/v1/attendance/ratio/history?room_id={room}");
        await hub.DisposeAsync();

        alerts.Should().ContainSingle("only the first shortage alerts, not every further arrival");
        alerts[0].GetProperty("room_id").GetGuid().Should().Be(room);
        history.Status.Should().Be(HttpStatusCode.OK);
        var entry = history.Items.Should().ContainSingle().Subject;
        entry.GetProperty("resolved_at").ValueKind.Should().NotBe(JsonValueKind.Null);
        entry.GetProperty("staff_required").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Shortage_history_is_for_managers_only()
    {
        var teacher = await api.CreateUserAsync("teacher");

        var reply = await teacher.Client.GetAsync("/api/v1/attendance/ratio/history");

        reply.Status.Should().Be(HttpStatusCode.Forbidden);
    }
}
