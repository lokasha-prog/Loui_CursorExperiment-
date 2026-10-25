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

    [Fact]
    public async Task A_childs_gallery_lists_only_their_photo_posts_and_only_for_their_own_family()
    {
        var mine = await FamilyAsync();
        var other = await FamilyAsync(mine.Room);
        var teacher = await TeacherOfAsync(mine.Room);
        var photo = await teacher.PostAsync("/api/v1/activities", Post("photo", mine.ChildId, "Painting", await UploadAsync(teacher)));
        await teacher.PostAsync("/api/v1/activities", Post("diary", mine.ChildId, "Slept well"));
        var gallery = $"/api/v1/activities?student_id={mine.ChildId}&status=approved&with_media=true";

        var seenByMine = await mine.Parent.GetAsync(gallery);
        var seenByStaff = await teacher.GetAsync(gallery);
        var seenByOther = await other.Parent.GetAsync(gallery);

        photo.Status.Should().Be(HttpStatusCode.Created, photo.Raw);
        seenByMine.Items.Should().ContainSingle(i => i.GetProperty("id").GetGuid() == photo.Data.GetProperty("id").GetGuid());
        seenByStaff.Items.Should().ContainSingle("the diary post has no photo");
        seenByOther.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_gallery_photo_can_be_removed_by_its_poster_or_a_manager_and_the_family_then_loses_it()
    {
        var family = await FamilyAsync();
        var teacher = await TeacherOfAsync(family.Room);
        var colleague = await TeacherOfAsync(family.Room);
        var media = await UploadAsync(teacher);
        var first = (await teacher.PostAsync("/api/v1/activities", Post("photo", family.ChildId, "Wrong child", media))).Data.GetProperty("id").GetGuid();
        var second = (await teacher.PostAsync("/api/v1/activities", Post("photo", family.ChildId, "Sand play", await UploadAsync(teacher)))).Data.GetProperty("id").GetGuid();
        (await family.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.OK);

        var byColleague = await colleague.DeleteAsync($"/api/v1/activities/{first}");
        var byParent = await family.Parent.DeleteAsync($"/api/v1/activities/{first}");
        var byPoster = await teacher.DeleteAsync($"/api/v1/activities/{first}");
        var byManager = await api.Admin.DeleteAsync($"/api/v1/activities/{second}");

        byColleague.Status.Should().Be(HttpStatusCode.Forbidden);
        byParent.Status.Should().Be(HttpStatusCode.Forbidden);
        byPoster.Status.Should().Be(HttpStatusCode.NoContent, byPoster.Raw);
        byManager.Status.Should().Be(HttpStatusCode.NoContent, byManager.Raw);
        (await family.Parent.GetAsync($"/api/v1/activities?student_id={family.ChildId}&with_media=true")).Items.Should().BeEmpty();
        (await family.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.NotFound);
        (await teacher.DeleteAsync($"/api/v1/activities/{first}")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_whole_nursery_photo_shows_in_every_childs_gallery_but_a_room_photo_only_in_that_rooms()
    {
        var first = await FamilyAsync();
        var second = await FamilyAsync();
        var media = await UploadAsync(api.Admin);
        var everyone = await api.Admin.PostAsync("/api/v1/activities", new { type = "photo", caption = new Dictionary<string, string> { ["en"] = "Our nursery" }, student_ids = Array.Empty<Guid>(), media_ids = new[] { media } });
        var roomOnly = await api.Admin.PostAsync("/api/v1/activities", new { type = "photo", caption = new Dictionary<string, string> { ["en"] = "Room trip" }, student_ids = Array.Empty<Guid>(), room_id = first.Room, media_ids = new[] { await UploadAsync(api.Admin) } });
        // A post for the whole centre reaches every family in the shared test database, so take it down again afterwards.
        try
        {
            string Gallery(Guid child) => $"/api/v1/activities?student_id={child}&status=approved&with_media=true&include_shared=true";

            var firstSees = await first.Parent.GetAsync(Gallery(first.ChildId));
            var secondSees = await second.Parent.GetAsync(Gallery(second.ChildId));
            var staffSees = await api.Admin.GetAsync(Gallery(second.ChildId));
            var withoutShared = await first.Parent.GetAsync($"/api/v1/activities?student_id={first.ChildId}&with_media=true");

            everyone.Status.Should().Be(HttpStatusCode.Created, everyone.Raw);
            roomOnly.Status.Should().Be(HttpStatusCode.Created, roomOnly.Raw);
            var ids = (ApiReply r) => r.Items.Select(i => i.GetProperty("id").GetGuid()).ToList();
            ids(firstSees).Should().Contain([everyone.Data.GetProperty("id").GetGuid(), roomOnly.Data.GetProperty("id").GetGuid()]);
            ids(secondSees).Should().Contain(everyone.Data.GetProperty("id").GetGuid()).And.NotContain(roomOnly.Data.GetProperty("id").GetGuid());
            ids(staffSees).Should().Contain(everyone.Data.GetProperty("id").GetGuid()).And.NotContain(roomOnly.Data.GetProperty("id").GetGuid());
            withoutShared.Items.Should().BeEmpty("without include_shared only posts naming the child are listed");
            (await second.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await api.Admin.DeleteAsync($"/api/v1/activities/{everyone.Data.GetProperty("id").GetGuid()}");
            await api.Admin.DeleteAsync($"/api/v1/activities/{roomOnly.Data.GetProperty("id").GetGuid()}");
        }
    }

    private async Task<Family> FamilyInGroupAsync(Guid room, Guid? group)
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var made = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new
            {
                first_name = "Noor", last_name = $"Family{Guid.NewGuid():N}"[..14], date_of_birth = DateTime.UtcNow.AddMonths(-30).ToString("yyyy-MM-dd"),
                gender = "female", room_id = room, group_id = group,
            },
            guardians = new[] { new { first_name = "Parent", last_name = "One", email, relationship = "mother", is_primary = true } },
        });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        var child = made.Data.GetProperty("id").GetGuid();
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        return new Family((await api.CreateUserAsync("parent", guardianId: guardian)).Client, child, room, email);
    }

    [Fact]
    public async Task A_photo_for_a_group_reaches_the_children_in_that_group_only()
    {
        var room = await api.CreateRoomAsync();
        var group = (await api.Admin.PostAsync("/api/v1/groups", new { name = new { en = $"Stars {Guid.NewGuid():N}" }, room_id = room })).Data.GetProperty("id").GetGuid();
        var empty = (await api.Admin.PostAsync("/api/v1/groups", new { name = new { en = $"Empty {Guid.NewGuid():N}" }, room_id = room })).Data.GetProperty("id").GetGuid();
        var inGroup = await FamilyInGroupAsync(room, group);
        var outside = await FamilyInGroupAsync(room, null);

        var posted = await api.Admin.PostAsync("/api/v1/activities", new { type = "photo", group_id = group, media_ids = new[] { await UploadAsync(api.Admin) } });
        var noChildren = await api.Admin.PostAsync("/api/v1/activities", new { type = "photo", group_id = empty, media_ids = new[] { await UploadAsync(api.Admin) } });
        string Gallery(Guid child) => $"/api/v1/activities?student_id={child}&with_media=true&include_shared=true";

        posted.Status.Should().Be(HttpStatusCode.Created, posted.Raw);
        posted.Data.GetProperty("student_ids").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(inGroup.ChildId);
        (await inGroup.Parent.GetAsync(Gallery(inGroup.ChildId))).Items.Should().ContainSingle();
        (await outside.Parent.GetAsync(Gallery(outside.ChildId))).Items.Should().BeEmpty();
        noChildren.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_group_owner_shares_photos_with_a_chosen_branch_which_gets_its_own_copy()
    {
        var owner = (await api.CreateUserAsync("super_admin")).Client;
        var made = await owner.PostAsync("/api/v1/group/centres", new { name = "Gallery Branch", name_arabic = "فرع", slug = $"gal-{Guid.NewGuid():N}"[..20] });
        var branchId = made.Data.GetProperty("id").GetGuid();
        var media = await UploadAsync(owner);

        var centres = await owner.GetAsync("/api/v1/group/centres");
        var shared = await owner.PostAsync("/api/v1/activities/centres", new { caption = new Dictionary<string, string> { ["en"] = "Open day" }, media_ids = new[] { media }, centre_ids = new[] { branchId } });
        var unknown = await owner.PostAsync("/api/v1/activities/centres", new { media_ids = new[] { media }, centre_ids = new[] { Guid.NewGuid() } });
        var teacher = await TeacherOfAsync(await api.CreateRoomAsync());
        var byTeacher = await teacher.PostAsync("/api/v1/activities/centres", new { media_ids = new[] { await UploadAsync(teacher) } });

        centres.Items.Select(c => c.GetProperty("id").GetGuid()).Should().Contain([api.TenantId, branchId]);
        shared.Status.Should().Be(HttpStatusCode.Created, shared.Raw);
        shared.Data.GetProperty("centres").GetInt32().Should().Be(1);
        unknown.Status.Should().Be(HttpStatusCode.BadRequest);
        byTeacher.Status.Should().Be(HttpStatusCode.Forbidden);

        var switched = await owner.PostAsync("/api/v1/group/switch", new { centre_id = branchId });
        var branch = api.NewClient();
        branch.SetBearer(switched.Data.GetProperty("access_token").GetString()!);
        var seen = (await branch.GetAsync("/api/v1/activities?with_media=true")).Items.Should().ContainSingle().Subject;
        seen.GetProperty("caption").GetProperty("en").GetString().Should().Be("Open day");
        var copy = seen.GetProperty("media")[0].GetProperty("id").GetGuid();
        copy.Should().NotBe(media, "each centre keeps its own copy of the file");
        (await branch.GetBytesAsync($"/api/v1/media/{copy}/content")).Bytes.Should().Equal(Png);
        var headCaptions = (await owner.GetAsync("/api/v1/activities?with_media=true")).Items
            .Select(i => i.GetProperty("caption").ValueKind == System.Text.Json.JsonValueKind.Object ? i.GetProperty("caption").ToString() : string.Empty).ToList();
        headCaptions.Should().NotContain(c => c.Contains("Open day"), "only the chosen branch was named");
    }

    [Fact]
    public async Task A_super_administrator_can_remove_a_shared_photo_someone_else_posted()
    {
        var family = await FamilyAsync();
        var teacher = await TeacherOfAsync(family.Room);
        var owner = (await api.CreateUserAsync("super_admin")).Client;
        var media = await UploadAsync(teacher);
        var shared = (await teacher.PostAsync("/api/v1/activities", new { type = "photo", room_id = family.Room, media_ids = new[] { media } })).Data.GetProperty("id").GetGuid();
        (await family.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.OK);

        var removed = await owner.DeleteAsync($"/api/v1/activities/{shared}");

        removed.Status.Should().Be(HttpStatusCode.NoContent, removed.Raw);
        (await family.Parent.GetAsync($"/api/v1/activities?student_id={family.ChildId}&with_media=true&include_shared=true")).Items.Should().BeEmpty();
        (await family.Parent.GetBytesAsync($"/api/v1/media/{media}/content")).Status.Should().Be(HttpStatusCode.NotFound);
    }
}
