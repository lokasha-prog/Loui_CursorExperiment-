using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NMS.Api.Jobs;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CommunicationTests(NmsApiFixture api)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3];

    private sealed record Family(ApiClient Parent, Guid ChildId, Guid Room, string Email);

    private async Task<Family> FamilyAsync(Guid? room = null)
    {
        room ??= await api.CreateRoomAsync();
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(room, email);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        return new Family(parent.Client, child, room.Value, email);
    }

    private async Task<ApiClient> TeacherOfAsync(Guid room)
    {
        var staff = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(staff, room);
        return (await api.CreateUserAsync("teacher", staffId: staff)).Client;
    }

    private static object Post(string type, Guid child, string text, params Guid[] media) =>
        new { type, caption = new Dictionary<string, string> { ["en"] = text }, student_ids = new[] { child }, media_ids = media };

    private async Task<Guid> UploadAsync(ApiClient client) =>
        (await client.PostFileAsync("/api/v1/media", Png, "photo.png")).Data.GetProperty("id").GetGuid();

    [Fact]
    public async Task Only_real_photos_and_videos_are_accepted_whatever_they_are_called()
    {
        var teacher = await TeacherOfAsync(await api.CreateRoomAsync());

        var ok = await teacher.PostFileAsync("/api/v1/media", Png, "photo.png");
        var script = await teacher.PostFileAsync("/api/v1/media", System.Text.Encoding.UTF8.GetBytes("MZ evil program"), "cute.png");
        var tooBig = await teacher.PostFileAsync("/api/v1/media", Png.Concat(new byte[11 * 1024 * 1024]).ToArray(), "huge.png");

        ok.Status.Should().Be(HttpStatusCode.Created, ok.Raw);
        ok.Data.GetProperty("content_type").GetString().Should().Be("image/png");
        script.Status.Should().Be(HttpStatusCode.BadRequest);
        tooBig.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_diary_post_reaches_the_tagged_childs_family_straight_away_and_no_one_elses()
    {
        var mine = await FamilyAsync();
        var other = await FamilyAsync(mine.Room);
        var teacher = await TeacherOfAsync(mine.Room);

        var created = await teacher.PostAsync("/api/v1/activities", Post("diary", mine.ChildId, "Built a tower"));
        var seenByMine = await mine.Parent.GetAsync("/api/v1/activities");
        var seenByOther = await other.Parent.GetAsync("/api/v1/activities");

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        created.Data.GetProperty("status").GetString().Should().Be("approved");
        seenByMine.Items.Should().ContainSingle(i => i.GetProperty("id").GetGuid() == created.Data.GetProperty("id").GetGuid());
        seenByOther.Items.Should().BeEmpty("the post names another child");
    }

    [Fact]
    public async Task An_announcement_waits_for_a_manager_and_media_opens_only_after_approval_and_only_for_that_family()
    {
        var mine = await FamilyAsync();
        var other = await FamilyAsync(mine.Room);
        var teacher = await TeacherOfAsync(mine.Room);
        var media = await UploadAsync(teacher);
        var created = await teacher.PostAsync("/api/v1/activities", Post("announcement", mine.ChildId, "Trip on Monday", media));
        var id = created.Data.GetProperty("id").GetGuid();

        created.Data.GetProperty("status").GetString().Should().Be("pending");
        (await mine.Parent.GetAsync("/api/v1/activities")).Items.Should().BeEmpty();
        (await mine.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.NotFound);
        (await teacher.PostAsync($"/api/v1/activities/{id}/actions/approve")).Status.Should().Be(HttpStatusCode.Forbidden);

        (await api.Admin.PostAsync($"/api/v1/activities/{id}/actions/approve")).Status.Should().Be(HttpStatusCode.OK);
        var file = await mine.Parent.GetBytesAsync($"/api/v1/media/{media}/content");

        (await mine.Parent.GetAsync("/api/v1/activities")).Items.Should().ContainSingle();
        file.Status.Should().Be(HttpStatusCode.OK);
        file.ContentType.Should().Be("image/png");
        file.Bytes.Should().Equal(Png);
        (await other.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.NotFound);
        (await api.Admin.PostAsync($"/api/v1/activities/{id}/actions/reject")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_rejected_post_never_reaches_families_and_photo_posts_need_a_file()
    {
        var family = await FamilyAsync();
        var teacher = await TeacherOfAsync(family.Room);
        var pending = await teacher.PostAsync("/api/v1/activities", Post("medical", family.ChildId, "Fell over, plaster applied"));
        var noFile = await teacher.PostAsync("/api/v1/activities", Post("photo", family.ChildId, "Look"));

        var rejected = await api.Admin.PostAsync($"/api/v1/activities/{pending.Data.GetProperty("id").GetGuid()}/actions/reject", new { reason = "Tell parents in person" });

        rejected.Data.GetProperty("status").GetString().Should().Be("rejected");
        (await family.Parent.GetAsync("/api/v1/activities")).Items.Should().BeEmpty();
        noFile.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_teacher_cannot_post_about_a_child_outside_their_rooms_and_parents_cannot_post()
    {
        var family = await FamilyAsync();
        var outsider = await TeacherOfAsync(await api.CreateRoomAsync());

        var denied = await outsider.PostAsync("/api/v1/activities", Post("diary", family.ChildId, "Hello"));
        var parentPost = await family.Parent.PostAsync("/api/v1/activities", Post("diary", family.ChildId, "Hello"));

        denied.Status.Should().Be(HttpStatusCode.BadRequest);
        parentPost.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_family_message_lands_in_the_shared_inbox_staff_reply_and_the_family_sees_it_unread_then_read()
    {
        var family = await FamilyAsync();
        var outsider = await FamilyAsync();
        var reception = await api.CreateUserAsync("receptionist", staffId: await api.CreateStaffAsync());

        var created = await family.Parent.PostAsync("/api/v1/messages/threads", new { subject = "Pick-up change", body = "Grandma collects today", student_id = family.ChildId });
        var id = created.Data.GetProperty("id").GetGuid();
        var inbox = await reception.Client.GetAsync("/api/v1/messages/threads");
        var reply = await reception.Client.PostAsync($"/api/v1/messages/threads/{id}/messages", new { body = "Noted, thank you" });
        var list = await family.Parent.GetAsync("/api/v1/messages/threads");
        var opened = await family.Parent.GetAsync($"/api/v1/messages/threads/{id}");
        var after = await family.Parent.GetAsync("/api/v1/messages/threads");

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        inbox.Items.Should().Contain(t => t.GetProperty("id").GetGuid() == id);
        reply.Status.Should().Be(HttpStatusCode.Created, reply.Raw);
        list.Items.Single().GetProperty("unread").GetBoolean().Should().BeTrue();
        opened.Data.GetProperty("messages").GetArrayLength().Should().Be(2);
        opened.Data.GetProperty("messages")[1].GetProperty("from_staff").GetBoolean().Should().BeTrue();
        after.Items.Single().GetProperty("unread").GetBoolean().Should().BeFalse("opening is the read receipt");
        (await outsider.Parent.GetAsync($"/api/v1/messages/threads/{id}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await outsider.Parent.PostAsync($"/api/v1/messages/threads/{id}/messages", new { body = "hi" })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Staff_can_message_a_family_and_broadcast_but_families_cannot_reply_to_broadcasts_or_send_them()
    {
        var family = await FamilyAsync();
        var noAccount = await api.CreateStudentAsync(family.Room, $"none-{Guid.NewGuid():N}@test.local");

        var direct = await api.Admin.PostAsync("/api/v1/messages/threads", new { subject = "Update", body = "Hello", student_id = family.ChildId });
        var unreachable = await api.Admin.PostAsync("/api/v1/messages/threads", new { subject = "Update", body = "Hello", student_id = noAccount });
        var broadcast = await api.Admin.PostAsync("/api/v1/messages/threads", new { subject = "Closed Friday", body = "Eid holiday", broadcast = true });
        var bid = broadcast.Data.GetProperty("id").GetGuid();

        direct.Status.Should().Be(HttpStatusCode.Created, direct.Raw);
        unreachable.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await family.Parent.GetAsync($"/api/v1/messages/threads/{bid}")).Data.GetProperty("can_reply").GetBoolean().Should().BeFalse();
        (await family.Parent.PostAsync($"/api/v1/messages/threads/{bid}/messages", new { body = "ok" })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await family.Parent.PostAsync("/api/v1/messages/threads", new { subject = "x", body = "y", broadcast = true })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Daily_reports_are_written_once_per_present_child_and_shown_only_to_that_family()
    {
        var mine = await FamilyAsync();
        var other = await FamilyAsync(mine.Room);
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{mine.ChildId}")).Data.GetProperty("barcode").GetString()!;
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });
        var jobs = api.Services.GetRequiredService<ScheduledJobsService>();

        await jobs.RunOnceAsync(CancellationToken.None);
        await jobs.RunOnceAsync(CancellationToken.None);
        var seen = await mine.Parent.GetAsync("/api/v1/daily-reports");
        var otherSeen = await other.Parent.GetAsync("/api/v1/daily-reports");

        var report = seen.Items.Should().ContainSingle().Subject;
        report.GetProperty("summary").GetProperty("ar").GetString().Should().NotBeNullOrEmpty();
        report.GetProperty("summary").GetProperty("en").GetString().Should().Contain("was with us today");
        otherSeen.Items.Should().BeEmpty();
    }
}
