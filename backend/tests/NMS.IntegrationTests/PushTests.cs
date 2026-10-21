using System.Net;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NMS.Application.Abstractions;
using NMS.Application.Notifications;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PushTests(NmsApiFixture api)
{
    private async Task DispatchAsync()
    {
        using var scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = api.TenantId;
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunNotificationDispatchCommand());
    }

    private async Task CauseShortageAsync()
    {
        var room = await api.CreateRoomAsync(ratio: 1);
        var child = await api.CreateStudentAsync(room);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{child}")).Data.GetProperty("barcode").GetString()!;
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
    }

    [Fact]
    public async Task A_phone_that_signed_in_gets_the_alert_and_stops_getting_it_after_it_is_removed()
    {
        var manager = await api.CreateUserAsync("centre_admin");
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";
        (await manager.Client.PostAsync("/api/v1/notifications/devices", new { token, platform = "android" })).Status.Should().Be(HttpStatusCode.NoContent);
        (await manager.Client.PostAsync("/api/v1/notifications/devices", new { token, platform = "android" })).Status.Should().Be(HttpStatusCode.NoContent);

        await CauseShortageAsync();
        await DispatchAsync();
        api.Push.Sent.Should().Contain(m => m.Token == token && m.Title.Contains("needs more staff"));
        api.Push.Meta.Should().Contain(m => m.Token == token && m.Tier == "critical");

        (await manager.Client.PostAsync("/api/v1/notifications/devices/actions/remove", new { token })).Status.Should().Be(HttpStatusCode.NoContent);
        api.Push.Sent.Clear();
        await CauseShortageAsync();
        await DispatchAsync();
        api.Push.Sent.Should().NotContain(m => m.Token == token);
    }

    [Fact]
    public async Task A_bad_platform_or_an_empty_token_is_refused()
    {
        var teacher = await api.CreateUserAsync("teacher");
        (await teacher.Client.PostAsync("/api/v1/notifications/devices", new { token = "", platform = "android" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await teacher.Client.PostAsync("/api/v1/notifications/devices", new { token = "abc", platform = "toaster" })).Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
