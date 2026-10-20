using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using NMS.Application.Abstractions;
using NMS.Application.Notifications;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SmsTests(NmsApiFixture api)
{
    private async Task<bool> SettingAsync(bool enabled, int quota = 200)
    {
        var current = (await api.Admin.GetAsync("/api/v1/settings/centre")).Data;
        var reply = await api.Admin.PutAsync("/api/v1/settings/centre", new
        {
            name = new { en = current.GetProperty("name").GetProperty("en").GetString() }, weekend_days = new[] { "fri", "sat" }, late_grace_minutes = 15, early_leave_grace_minutes = 15,
            parents_see_invoices = true, sms_enabled = enabled, sms_monthly_quota = quota,
        });
        return reply.Status == HttpStatusCode.OK;
    }

    private async Task DispatchAsync()
    {
        using var scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = api.TenantId;
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunNotificationDispatchCommand());
    }

    private async Task<string> ShortageAsync(string phone)
    {
        var staff = await api.CreateStaffAsync(phone: phone);
        var manager = await api.CreateUserAsync("centre_admin", staffId: staff);
        var room = await api.CreateRoomAsync(ratio: 1);
        var child = await api.CreateStudentAsync(room);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{child}")).Data.GetProperty("barcode").GetString()!;
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
        _ = manager;
        return phone;
    }

    [Fact]
    public async Task A_critical_alert_is_also_texted_when_the_centre_turns_texts_on_and_never_before()
    {
        await SettingAsync(false);
        api.Sms.Sent.Clear();
        var off = $"+9715{Random.Shared.Next(10_000_000, 99_999_999)}";
        await ShortageAsync(off);
        await DispatchAsync();
        api.Sms.Sent.Should().NotContain(m => m.To == off);

        await SettingAsync(true);
        var on = $"+9715{Random.Shared.Next(10_000_000, 99_999_999)}";
        await ShortageAsync(on);
        await DispatchAsync();
        await SettingAsync(false);

        api.Sms.Sent.Should().Contain(m => m.To == on && m.Text.Contains("needs more staff"));
    }

    [Fact]
    public async Task The_monthly_limit_stops_texts_and_a_centre_without_a_provider_sends_none()
    {
        await SettingAsync(true, quota: 0);
        api.Sms.Sent.Clear();
        var capped = $"+9715{Random.Shared.Next(10_000_000, 99_999_999)}";
        await ShortageAsync(capped);
        await DispatchAsync();
        api.Sms.Sent.Should().NotContain(m => m.To == capped);

        await SettingAsync(true, quota: 100);
        api.Sms.Configured = false;
        var unset = $"+9715{Random.Shared.Next(10_000_000, 99_999_999)}";
        await ShortageAsync(unset);
        await DispatchAsync();
        var settings = (await api.Admin.GetAsync("/api/v1/settings/centre")).Data;
        api.Sms.Configured = true;
        await SettingAsync(false);

        api.Sms.Sent.Should().NotContain(m => m.To == unset);
        settings.GetProperty("sms_available").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Texts_are_a_setting_only_for_those_who_may_change_settings_and_the_quota_is_checked()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var tooMany = await api.Admin.PutAsync("/api/v1/settings/centre", new { name = new { en = "X" }, weekend_days = new[] { "fri" }, late_grace_minutes = 15, early_leave_grace_minutes = 15, parents_see_invoices = true, sms_enabled = true, sms_monthly_quota = 5_000_000 });
        var denied = await teacher.Client.PutAsync("/api/v1/settings/centre", new { name = new { en = "X" }, weekend_days = new[] { "fri" }, late_grace_minutes = 15, early_leave_grace_minutes = 15, parents_see_invoices = true, sms_enabled = true });

        tooMany.Status.Should().Be(HttpStatusCode.BadRequest);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }
}
