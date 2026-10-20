using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CalendarTests(NmsApiFixture api)
{
    private static object Event(string title, string startsAt, string endsAt, string type = "regular", string? recurrence = null, bool allDay = false, string? visibility = null, Guid? room = null) => new
    {
        title = new { en = title, ar = "حدث" }, starts_at = startsAt, ends_at = endsAt, is_all_day = allDay, type, recurrence, visibility, room_id = room,
    };

    [Fact]
    public async Task A_weekly_event_shows_on_each_chosen_day_and_bad_input_is_refused()
    {
        var made = await api.Admin.PostAsync("/api/v1/calendar/events", Event("Story time", "2031-05-06T08:00:00+04:00", "2031-05-06T09:00:00+04:00", recurrence: "FREQ=WEEKLY;BYDAY=TU,TH"));
        var list = await api.Admin.GetAsync("/api/v1/calendar/events?from=2031-05-01&to=2031-05-14");
        var backwards = await api.Admin.PostAsync("/api/v1/calendar/events", Event("X", "2031-05-06T09:00:00+04:00", "2031-05-06T08:00:00+04:00"));
        var oddRule = await api.Admin.PostAsync("/api/v1/calendar/events", Event("X", "2031-05-06T08:00:00+04:00", "2031-05-06T09:00:00+04:00", recurrence: "FREQ=YEARLY"));
        var wide = await api.Admin.GetAsync("/api/v1/calendar/events?from=2031-01-01&to=2033-01-01");

        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        var days = list.Data.EnumerateArray().Where(o => o.GetProperty("title").GetProperty("en").GetString() == "Story time").Select(o => o.GetProperty("starts_at").GetString()![..10]).ToList();
        days.Should().Equal("2031-05-06", "2031-05-08", "2031-05-13");
        backwards.Status.Should().Be(HttpStatusCode.BadRequest);
        oddRule.Status.Should().Be(HttpStatusCode.BadRequest);
        wide.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Families_see_only_parent_visible_events_and_cannot_edit_the_calendar()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var teacher = await api.CreateUserAsync("teacher");
        await api.Admin.PostAsync("/api/v1/calendar/events", Event("Sports day", "2032-03-10T09:00:00+04:00", "2032-03-10T11:00:00+04:00"));
        await api.Admin.PostAsync("/api/v1/calendar/events", Event("Staff meeting", "2032-03-10T13:00:00+04:00", "2032-03-10T14:00:00+04:00", type: "staff_only"));

        var asParent = await parent.Client.GetAsync("/api/v1/calendar/events?from=2032-03-01&to=2032-03-31");
        var asTeacher = await teacher.Client.GetAsync("/api/v1/calendar/events?from=2032-03-01&to=2032-03-31");
        var parentWrite = await parent.Client.PostAsync("/api/v1/calendar/events", Event("Mine", "2032-03-11T09:00:00+04:00", "2032-03-11T10:00:00+04:00"));
        var teacherWrite = await teacher.Client.PostAsync("/api/v1/calendar/events", Event("Mine", "2032-03-11T09:00:00+04:00", "2032-03-11T10:00:00+04:00"));

        asParent.Data.EnumerateArray().Select(o => o.GetProperty("title").GetProperty("en").GetString()).Should().Contain("Sports day").And.NotContain("Staff meeting");
        asTeacher.Data.EnumerateArray().Select(o => o.GetProperty("title").GetProperty("en").GetString()).Should().Contain("Staff meeting");
        parentWrite.Status.Should().Be(HttpStatusCode.Forbidden);
        teacherWrite.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_holiday_means_no_expected_attendance_that_day()
    {
        await api.Admin.PostAsync("/api/v1/calendar/events", Event("National Day", "2033-12-02T00:00:00+04:00", "2033-12-02T23:59:00+04:00", type: "holiday", allDay: true));

        var holiday = await api.Admin.GetAsync("/api/v1/attendance/forecast?date=2033-12-02");
        var normal = await api.Admin.GetAsync("/api/v1/attendance/forecast?date=2033-12-03");

        holiday.Data.GetProperty("total_expected").GetInt32().Should().Be(0);
        holiday.Data.GetProperty("holiday").GetProperty("en").GetString().Should().Be("National Day");
        normal.Data.TryGetProperty("holiday", out var none).Should().BeTrue();
        none.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task The_agenda_combines_layers_and_birthdays_use_the_right_age()
    {
        var kid = await api.CreateStudentAsync(null, dateOfBirth: "2022-08-15");
        await api.Admin.PostAsync($"/api/v1/students/{kid}/actions/transition-status", new { status = "active" });
        await api.Admin.PostAsync("/api/v1/calendar/events", Event("Picnic", "2033-08-15T09:00:00+04:00", "2033-08-15T12:00:00+04:00"));

        var all = await api.Admin.GetAsync("/api/v1/calendar/agenda?from=2033-08-10&to=2033-08-20");
        var onlyEvents = await api.Admin.GetAsync("/api/v1/calendar/agenda?from=2033-08-10&to=2033-08-20&layers=events");
        var badLayer = await api.Admin.GetAsync("/api/v1/calendar/agenda?from=2033-08-10&to=2033-08-20&layers=dreams");

        var birthday = all.Data.EnumerateArray().First(i => i.GetProperty("kind").GetString() == "birthdays_students" && i.GetProperty("ref_id").GetGuid() == kid);
        birthday.GetProperty("detail").GetString().Should().Be("11");
        all.Data.EnumerateArray().Select(i => i.GetProperty("kind").GetString()).Should().Contain("event");
        onlyEvents.Data.EnumerateArray().Select(i => i.GetProperty("kind").GetString()).Should().OnlyContain(k => k == "event" || k == "holiday");
        badLayer.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_subscription_feed_needs_its_secret_lists_only_public_events_and_can_be_switched_off()
    {
        var soon = DateTimeOffset.UtcNow.AddDays(30);
        await api.Admin.PostAsync("/api/v1/calendar/events", Event("Open morning", soon.ToString("o"), soon.AddHours(1).ToString("o")));
        await api.Admin.PostAsync("/api/v1/calendar/events", Event("Staff only secret", soon.AddHours(2).ToString("o"), soon.AddHours(3).ToString("o"), type: "staff_only"));
        var link = (await api.Admin.GetAsync("/api/v1/calendar/feed-link")).Data.GetProperty("path").GetString()!;
        var anon = api.NewClient();

        var feed = await anon.GetBytesAsync(link);
        var wrong = await anon.GetBytesAsync(link[..^10] + "0000000.ics");
        var rotated = await api.Admin.PostAsync("/api/v1/calendar/feed-link/actions/rotate");
        var old = await anon.GetBytesAsync(link);

        var text = System.Text.Encoding.UTF8.GetString(feed.Bytes);
        feed.Status.Should().Be(HttpStatusCode.OK);
        text.Should().StartWith("BEGIN:VCALENDAR").And.Contain("SUMMARY:Open morning").And.NotContain("Staff only secret");
        wrong.Status.Should().Be(HttpStatusCode.NotFound);
        rotated.Data.GetProperty("path").GetString().Should().NotBe(link);
        old.Status.Should().Be(HttpStatusCode.NotFound);
    }
}
