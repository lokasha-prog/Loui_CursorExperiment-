using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LessonAssistTests(NmsApiFixture api)
{
    private async Task<(Guid Fw, Guid Area, Guid Band)> FrameworkAsync()
    {
        var code = $"fw-{Guid.NewGuid():N}"[..12];
        var fw = (await api.Admin.PostAsync("/api/v1/learning/frameworks", new { code, name = new { en = "Test" } })).Data.GetProperty("id").GetGuid();
        var tree = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{fw}/import", new
        {
            areas = new[] { new { code = "m", name = new { en = "Mathematics" } } },
            age_bands = new[] { new { code = "b1", name = new { en = "22-36 months" }, min_months = 22, max_months = 36 } },
            milestones = new[]
            {
                new { area = "m", age_band = "b1", code = "m1", statement = new { en = "Counts a few objects and recognises numerals" } },
                new { area = "m", age_band = "b1", code = "m2", statement = new { en = "Sorts objects by colour or size" } },
            },
        });
        return (fw, tree.Data.GetProperty("areas")[0].GetProperty("id").GetGuid(), tree.Data.GetProperty("age_bands")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Milestones_are_suggested_from_the_words_of_an_observation_without_any_outside_service()
    {
        var f = await FrameworkAsync();
        api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("unused");

        var reply = await api.Admin.GetAsync($"/api/v1/learning/suggest-milestones?framework_id={f.Fw}&text=She counted five stones and sorted them by colour");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetArrayLength().Should().Be(2);
        reply.Data.EnumerateArray().Select(x => x.GetProperty("code").GetString()).Should().BeEquivalentTo("m1", "m2");
        api.Ai.LastUser.Should().NotContain("stones");
    }

    [Fact]
    public async Task A_lesson_idea_comes_from_the_AI_using_only_the_curriculum_names_and_a_theme()
    {
        var f = await FrameworkAsync();
        api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("TITLE: Number picnic\nDESCRIPTION: Children count fruit. They share it out.\nMATERIALS: plastic fruit; baskets; number cards\nOBJECTIVES: count to five; match numerals");

        var reply = await api.Admin.GetAsync($"/api/v1/ai/lesson-draft?framework_id={f.Fw}&area_id={f.Area}&age_band_id={f.Band}&theme=Picnic&language=en");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetProperty("title").GetProperty("en").GetString().Should().Be("Number picnic");
        reply.Data.GetProperty("materials").GetArrayLength().Should().Be(3);
        reply.Data.GetProperty("objectives").GetArrayLength().Should().Be(2);
        api.Ai.LastUser.Should().Contain("Mathematics").And.Contain("22-36 months").And.Contain("Picnic");

        api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("I cannot help");
        (await api.Admin.GetAsync($"/api/v1/ai/lesson-draft?framework_id={f.Fw}")).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("Attendance was steady.");
    }

    [Fact]
    public async Task Templates_are_kept_apart_from_ordinary_lessons_and_parents_cannot_use_the_helpers()
    {
        var f = await FrameworkAsync();
        await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Plan for toddlers" }, framework_id = f.Fw, is_template = true });
        await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Tuesday lesson" }, framework_id = f.Fw });
        var parent = await api.CreateUserAsync("parent");

        var templates = await api.Admin.GetAsync($"/api/v1/learning/lessons?framework_id={f.Fw}&templates=true");
        var lessons = await api.Admin.GetAsync($"/api/v1/learning/lessons?framework_id={f.Fw}");

        templates.Items.Select(x => x.GetProperty("title").GetProperty("en").GetString()).Should().Equal("Plan for toddlers");
        lessons.Items.Select(x => x.GetProperty("title").GetProperty("en").GetString()).Should().Equal("Tuesday lesson");
        (await parent.Client.GetAsync($"/api/v1/learning/suggest-milestones?framework_id={f.Fw}&text=x")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await parent.Client.GetAsync($"/api/v1/ai/lesson-draft?framework_id={f.Fw}")).Status.Should().Be(HttpStatusCode.Forbidden);
    }
}
