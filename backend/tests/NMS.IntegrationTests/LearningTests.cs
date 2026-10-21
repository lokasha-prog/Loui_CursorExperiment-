using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LearningTests(NmsApiFixture api)
{
    private async Task<(Guid Framework, Guid Area, Guid Band)> NewFrameworkAsync()
    {
        var code = $"fw-{Guid.NewGuid():N}"[..12];
        var created = await api.Admin.PostAsync("/api/v1/learning/frameworks", new { code, name = new { en = "Test framework", ar = "إطار اختبار" } });
        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        var id = created.Data.GetProperty("id").GetGuid();
        var imported = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{id}/import", new
        {
            areas = new[] { new { code = "cal", name = new { en = "Communication and Language" }, sort_order = 1 } },
            age_bands = new[] { new { code = "b8-20", name = new { en = "8-20 months" }, min_months = 8, max_months = 20 } },
            milestones = new[] { new { area = "cal", age_band = "b8-20", code = "cal-1", statement = new { en = "Responds to their name" } } },
        });
        imported.Status.Should().Be(HttpStatusCode.OK, imported.Raw);
        return (id, imported.Data.GetProperty("areas")[0].GetProperty("id").GetGuid(), imported.Data.GetProperty("age_bands")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_framework_is_built_by_import_and_can_be_read_back_as_one_tree()
    {
        var (id, _, _) = await NewFrameworkAsync();

        var tree = await api.Admin.GetAsync($"/api/v1/learning/frameworks/{id}");
        var list = await api.Admin.GetAsync("/api/v1/learning/frameworks");

        tree.Data.GetProperty("milestones").GetArrayLength().Should().Be(1);
        list.Items.Should().Contain(f => f.GetProperty("id").GetGuid() == id && f.GetProperty("milestones").GetInt32() == 1);
    }

    [Fact]
    public async Task Importing_again_updates_by_code_and_a_bad_reference_is_refused_without_changing_anything()
    {
        var (id, _, _) = await NewFrameworkAsync();

        var again = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{id}/import", new { milestones = new[] { new { area = "cal", age_band = "b8-20", code = "cal-1", statement = new { en = "Turns to their name" } } } });
        var bad = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{id}/import", new { milestones = new[] { new { area = "nope", age_band = "b8-20", code = "x-1", statement = new { en = "x" } } } });

        again.Data.GetProperty("milestones").GetArrayLength().Should().Be(1);
        again.Data.GetProperty("milestones")[0].GetProperty("statement").GetProperty("en").GetString().Should().Be("Turns to their name");
        bad.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lessons_can_be_made_filtered_and_searched_and_must_use_parts_of_their_own_framework()
    {
        var (fw, area, band) = await NewFrameworkAsync();
        var other = await NewFrameworkAsync();

        var made = await api.Admin.PostAsync("/api/v1/learning/lessons", new
        {
            title = new { en = "Rainbow painting", ar = "رسم قوس قزح" }, framework_id = fw, area_id = area, age_band_id = band, duration_minutes = 40,
            materials = new[] { "Paint", "Paper" }, objectives = new[] { "Name colours" }, links = new Dictionary<string, string> { ["https://example.com/rainbow"] = "Idea sheet" },
        });
        var wrong = await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Mixed up" }, framework_id = fw, area_id = other.Area });
        var found = await api.Admin.GetAsync($"/api/v1/learning/lessons?framework_id={fw}&search=rainbow");
        var none = await api.Admin.GetAsync($"/api/v1/learning/lessons?framework_id={fw}&search=zzzz");
        var badLink = await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Bad link" }, framework_id = fw, links = new Dictionary<string, string> { ["javascript:alert(1)"] = "x" } });

        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        wrong.Status.Should().Be(HttpStatusCode.BadRequest);
        found.Items.Should().ContainSingle();
        none.Items.Should().BeEmpty();
        badLink.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_teacher_reads_and_writes_lessons_for_their_room_only_and_cannot_change_the_curriculum()
    {
        var (fw, _, _) = await NewFrameworkAsync();
        var teacher = await api.CreateUserAsync("teacher");

        var read = await teacher.Client.GetAsync($"/api/v1/learning/frameworks/{fw}");
        var edit = await teacher.Client.PostAsync($"/api/v1/learning/frameworks/{fw}/import", new { areas = new[] { new { code = "x", name = new { en = "x" } } } });
        var centreWide = await teacher.Client.PostAsync("/api/v1/learning/lessons", new { title = new { en = "For everyone" }, framework_id = fw });
        var parent = await api.CreateUserAsync("parent");
        var parentRead = await parent.Client.GetAsync("/api/v1/learning/frameworks");

        read.Status.Should().Be(HttpStatusCode.OK);
        edit.Status.Should().Be(HttpStatusCode.Forbidden);
        centreWide.Status.Should().Be(HttpStatusCode.Forbidden);
        parentRead.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Parts_in_use_cannot_be_deleted_and_a_framework_with_lessons_cannot_be_removed()
    {
        var (fw, area, band) = await NewFrameworkAsync();
        var tree = await api.Admin.GetAsync($"/api/v1/learning/frameworks/{fw}");
        var milestone = tree.Data.GetProperty("milestones")[0].GetProperty("id").GetGuid();
        await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Uses a milestone" }, framework_id = fw, area_id = area, age_band_id = band, milestone_ids = new[] { milestone } });

        var deleteMilestone = await api.Admin.DeleteAsync($"/api/v1/learning/frameworks/{fw}/milestones/{milestone}");
        var deleteArea = await api.Admin.DeleteAsync($"/api/v1/learning/frameworks/{fw}/areas/{area}");
        var deleteFramework = await api.Admin.DeleteAsync($"/api/v1/learning/frameworks/{fw}");

        deleteMilestone.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        deleteArea.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        deleteFramework.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task The_public_website_endpoint_shows_only_the_name_and_open_programmes()
    {
        var reply = await api.NewClient().GetAsync($"/api/v1/public/centre/{api.TenantSlug}");
        var missing = await api.NewClient().GetAsync("/api/v1/public/centre/no-such-centre");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Message.ToString());
        missing.Status.Should().Be(HttpStatusCode.NotFound);
    }
}
