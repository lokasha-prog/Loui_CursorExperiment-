using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TourTests(NmsApiFixture api)
{
    private static string At(int days, int hour) => DateTimeOffset.UtcNow.Date.AddDays(days).AddHours(hour).ToString("o");

    private async Task<(Guid Id, string Email)> EnquiryAsync()
    {
        var email = $"tour-{Guid.NewGuid():N}@test.local";
        var r = await api.Admin.PostAsync("/api/v1/enquiries", new { family_name = "Mansour", child_name = "Lina Mansour", phone = "+971500000001", email, source = "walk_in" });
        r.Status.Should().Be(HttpStatusCode.Created, r.Raw);
        return (r.Data.GetProperty("enquiry").GetProperty("id").GetGuid(), email);
    }

    private async Task RunAsync(object command)
    {
        using var scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<NMS.Application.Abstractions.ITenantContext>().TenantId = api.TenantId;
        await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(command);
    }

    [Fact]
    public async Task Booking_a_tour_moves_the_enquiry_and_sends_a_bilingual_confirmation()
    {
        var (enquiry, email) = await EnquiryAsync();
        var staff = await api.CreateStaffAsync();
        api.Email.Sent.Clear();

        var booked = await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = enquiry, starts_at = At(3, 10), staff_id = staff, adults = 2, children = 1 });
        await RunAsync(new NMS.Application.Enquiries.RunOutboundEmailCommand());
        var detail = await api.Admin.GetAsync($"/api/v1/enquiries/{enquiry}");

        booked.Status.Should().Be(HttpStatusCode.Created, booked.Raw);
        detail.Data.GetProperty("enquiry").GetProperty("stage").GetString().Should().Be("tour_appointment");
        api.Email.Sent.Should().Contain(m => m.To == email && m.Subject.Contains("booked") && m.Subject.Contains("تم حجز"));
    }

    [Fact]
    public async Task A_person_cannot_be_double_booked_or_booked_in_a_blocked_time_or_in_the_past()
    {
        var (a, _) = await EnquiryAsync();
        var (b, _) = await EnquiryAsync();
        var staff = await api.CreateStaffAsync();
        await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = a, starts_at = At(4, 9), staff_id = staff, duration_minutes = 60 });
        var block = await api.Admin.PostAsync("/api/v1/tours/block-offs", new { staff_id = staff, starts_at = At(4, 12), ends_at = At(4, 13), reason = "Lunch" });

        var overlap = await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = b, starts_at = DateTimeOffset.UtcNow.Date.AddDays(4).AddHours(9).AddMinutes(30).ToString("o"), staff_id = staff });
        var blocked = await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = b, starts_at = At(4, 12), staff_id = staff });
        var past = await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = b, starts_at = At(-2, 9), staff_id = staff });
        var free = await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = b, starts_at = At(4, 10), staff_id = staff });

        block.Status.Should().Be(HttpStatusCode.Created, block.Raw);
        overlap.Status.Should().Be(HttpStatusCode.Conflict);
        blocked.Status.Should().Be(HttpStatusCode.Conflict);
        past.Status.Should().Be(HttpStatusCode.BadRequest);
        free.Status.Should().Be(HttpStatusCode.Created, free.Raw);
    }

    [Fact]
    public async Task Completing_a_tour_marks_the_enquiry_toured_and_creates_a_follow_up_task_that_can_be_done()
    {
        var (enquiry, _) = await EnquiryAsync();
        var staff = await api.CreateStaffAsync();
        var tour = (await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = enquiry, starts_at = At(5, 11), staff_id = staff })).Data.GetProperty("id").GetGuid();

        var done = await api.Admin.PostAsync($"/api/v1/tours/{tour}/actions/set-status", new { status = "completed" });
        var again = await api.Admin.PostAsync($"/api/v1/tours/{tour}/actions/set-status", new { status = "no_show" });
        var detail = await api.Admin.GetAsync($"/api/v1/enquiries/{enquiry}");
        var tasks = await api.Admin.GetAsync($"/api/v1/follow-up-tasks?staff_id={staff}&limit=50");
        var task = tasks.Items.Single(t => t.GetProperty("enquiry_id").GetGuid() == enquiry);
        var closed = await api.Admin.PostAsync($"/api/v1/follow-up-tasks/{task.GetProperty("id").GetGuid()}/actions/complete");

        done.Status.Should().Be(HttpStatusCode.OK, done.Raw);
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        detail.Data.GetProperty("enquiry").GetProperty("stage").GetString().Should().Be("toured");
        closed.Data.GetProperty("is_done").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_reminder_goes_out_once_for_a_tour_within_24_hours()
    {
        var (enquiry, email) = await EnquiryAsync();
        var soon = DateTimeOffset.UtcNow.AddHours(5).ToString("o");
        await api.Admin.PostAsync("/api/v1/tours", new { enquiry_id = enquiry, starts_at = soon });
        await RunAsync(new NMS.Application.Enquiries.RunOutboundEmailCommand());
        api.Email.Sent.Clear();

        await RunAsync(new NMS.Application.Enquiries.RunTourRemindersCommand());
        await RunAsync(new NMS.Application.Enquiries.RunTourRemindersCommand());
        await RunAsync(new NMS.Application.Enquiries.RunOutboundEmailCommand());

        api.Email.Sent.Where(m => m.To == email && m.Subject.Contains("Reminder")).Should().ContainSingle();
    }
}
