using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class FormTests(NmsApiFixture api)
{
    private static object Field(string id, string type, string label, bool required = false, object[]? options = null, string? showWhen = null, string? equals = null) => new
    {
        id, type, label = new { en = label, ar = label + " ar" }, required, options = options ?? Array.Empty<object>(), show_when_field_id = showWhen, show_when_equals = equals,
    };

    private static object[] Fields() =>
    [
        Field("intro", "section", "About your child"),
        Field("child_name", "text", "Child name", required: true),
        Field("age", "number", "Age"),
        Field("start", "date", "Start date"),
        Field("transport", "dropdown", "Transport", options: [new { value = "bus", label = new { en = "Bus" } }, new { value = "own", label = new { en = "Own" } }]),
        Field("bus_stop", "text", "Bus stop", required: true, showWhen: "transport", equals: "bus"),
        Field("agree", "checkbox", "I agree", required: true),
    ];

    private async Task<Guid> LiveFormAsync(bool isPublic = false, string type = "admission")
    {
        var made = await api.Admin.PostAsync("/api/v1/forms", new { name = new { en = $"Form {Guid.NewGuid():N}"[..14], ar = "نموذج" }, type, is_public = isPublic });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        var id = made.Data.GetProperty("form").GetProperty("id").GetGuid();
        (await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = Fields() })).Status.Should().Be(HttpStatusCode.OK);
        (await api.Admin.PostAsync($"/api/v1/forms/{id}/actions/publish")).Status.Should().Be(HttpStatusCode.OK);
        return id;
    }

    [Fact]
    public async Task A_form_is_built_published_and_only_form_managers_can_do_that()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var made = await api.Admin.PostAsync("/api/v1/forms", new { name = new { en = "Trip permission" }, type = "custom", is_public = false });
        var id = made.Data.GetProperty("form").GetProperty("id").GetGuid();
        var emptyPublish = await api.Admin.PostAsync($"/api/v1/forms/{id}/actions/publish");
        var saved = await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = Fields() });
        var published = await api.Admin.PostAsync($"/api/v1/forms/{id}/actions/publish");
        var again = await api.Admin.PostAsync($"/api/v1/forms/{id}/actions/publish");
        var denied = await teacher.Client.PostAsync("/api/v1/forms", new { name = new { en = "Nope" }, type = "custom", is_public = false });

        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        emptyPublish.Status.Should().Be(HttpStatusCode.BadRequest);
        saved.Data.GetProperty("has_unpublished_changes").GetBoolean().Should().BeTrue();
        published.Data.GetProperty("form").GetProperty("status").GetString().Should().Be("live");
        published.Data.GetProperty("form").GetProperty("published_version").GetInt32().Should().Be(1);
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_bad_field_list_is_refused_with_a_reason()
    {
        var made = await api.Admin.PostAsync("/api/v1/forms", new { name = new { en = "Bad one" }, type = "custom", is_public = false });
        var id = made.Data.GetProperty("form").GetProperty("id").GetGuid();

        var duplicate = await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = new[] { Field("a", "text", "A"), Field("a", "text", "B") } });
        var unknown = await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = new[] { Field("a", "hologram", "A") } });
        var noOptions = await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = new[] { Field("a", "dropdown", "A") } });
        var rulePointsDown = await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = new[] { Field("a", "text", "A", showWhen: "b", equals: "x"), Field("b", "text", "B") } });

        foreach (var reply in new[] { duplicate, unknown, noOptions, rulePointsDown }) reply.Status.Should().Be(HttpStatusCode.BadRequest, reply.Raw);
    }

    [Fact]
    public async Task Answers_are_checked_conditional_questions_apply_and_the_submission_is_stored()
    {
        var id = await LiveFormAsync();
        var kid = await api.CreateStudentAsync(await api.CreateRoomAsync());

        var good = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["age"] = 3, ["start"] = "2026-11-01", ["transport"] = "own", ["agree"] = true }, student_id = kid });
        var missingStop = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["transport"] = "bus", ["agree"] = true } });
        var notAgreed = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["agree"] = false } });
        var badDate = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["start"] = "soon", ["agree"] = true } });
        var badChoice = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["transport"] = "helicopter", ["agree"] = true } });
        var stranger = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["agree"] = true, ["salary"] = "1" } });
        var list = await api.Admin.GetAsync($"/api/v1/forms/{id}/submissions");

        good.Status.Should().Be(HttpStatusCode.Created, good.Raw);
        foreach (var bad in new[] { missingStop, notAgreed, badDate, badChoice, stranger }) bad.Status.Should().Be(HttpStatusCode.BadRequest, bad.Raw);
        missingStop.Raw.Should().Contain("answers.bus_stop");
        var row = list.Items.Single();
        row.GetProperty("answers").GetProperty("child_name").GetString().Should().Be("Sara");
        row.GetProperty("student_id").GetGuid().Should().Be(kid);
    }

    [Fact]
    public async Task Editing_a_live_form_makes_a_new_draft_and_people_keep_seeing_the_published_version_until_publish()
    {
        var id = await LiveFormAsync();
        var edit = await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = new[] { Field("only", "text", "Only question", required: true) } });
        var beforePublish = await api.Admin.GetAsync($"/api/v1/forms/{id}/fill");
        var oldStillWorks = await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["agree"] = true } });
        await api.Admin.PostAsync($"/api/v1/forms/{id}/actions/publish");
        var afterPublish = await api.Admin.GetAsync($"/api/v1/forms/{id}/fill");

        edit.Data.GetProperty("editing_version").GetInt32().Should().Be(2);
        beforePublish.Data.GetProperty("version").GetInt32().Should().Be(1);
        oldStillWorks.Status.Should().Be(HttpStatusCode.Created, oldStillWorks.Raw);
        afterPublish.Data.GetProperty("version").GetInt32().Should().Be(2);
        afterPublish.Data.GetProperty("fields").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Families_see_only_live_forms_can_fill_them_and_only_for_their_own_child()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var other = await api.CreateStudentAsync(await api.CreateRoomAsync());
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var live = await LiveFormAsync();
        var draft = (await api.Admin.PostAsync("/api/v1/forms", new { name = new { en = "Secret draft" }, type = "custom", is_public = false })).Data.GetProperty("form").GetProperty("id").GetGuid();

        var list = await parent.Client.GetAsync("/api/v1/forms?limit=100");
        var own = await parent.Client.PostAsync($"/api/v1/forms/{live}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["agree"] = true }, student_id = child });
        var foreign = await parent.Client.PostAsync($"/api/v1/forms/{live}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["agree"] = true }, student_id = other });
        var seeAnswers = await parent.Client.GetAsync($"/api/v1/forms/{live}/submissions");
        var seeDraft = await parent.Client.GetAsync($"/api/v1/forms/{draft}/fill");

        var ids = list.Items.Select(i => i.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(live).And.NotContain(draft);
        own.Status.Should().Be(HttpStatusCode.Created, own.Raw);
        foreign.Status.Should().Be(HttpStatusCode.BadRequest);
        seeAnswers.Status.Should().Be(HttpStatusCode.Forbidden);
        seeDraft.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_public_form_works_without_signing_in_and_a_private_one_does_not()
    {
        var open = await LiveFormAsync(isPublic: true);
        var closed = await LiveFormAsync(isPublic: false);
        var anon = api.NewClient();

        var form = await anon.GetAsync($"/api/v1/public/forms/{api.TenantSlug}/{open}");
        var sent = await anon.PostAsync($"/api/v1/public/forms/{api.TenantSlug}/{open}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Noor", ["agree"] = true } });
        var linked = await anon.PostAsync($"/api/v1/public/forms/{api.TenantSlug}/{open}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Noor", ["agree"] = true }, student_id = Guid.NewGuid() });
        var hidden = await anon.GetAsync($"/api/v1/public/forms/{api.TenantSlug}/{closed}");
        var count = await api.Admin.GetAsync($"/api/v1/forms/{open}/submissions");

        form.Status.Should().Be(HttpStatusCode.OK, form.Raw);
        sent.Status.Should().Be(HttpStatusCode.Created, sent.Raw);
        hidden.Status.Should().Be(HttpStatusCode.NotFound);
        count.Items.Should().HaveCount(1);
        linked.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Analytics_count_starts_completion_time_and_where_people_give_up()
    {
        var id = await LiveFormAsync();
        async Task<Guid> Start() => (await api.Admin.PostAsync($"/api/v1/forms/{id}/starts")).Data.GetProperty("start_id").GetGuid();

        var finished = await Start();
        await api.Admin.PostAsync($"/api/v1/forms/{id}/submissions", new { answers = new Dictionary<string, object> { ["child_name"] = "Sara", ["agree"] = true }, start_id = finished });
        foreach (var field in new[] { "age", "age", "start" })
        {
            var quitter = await Start();
            await api.Admin.PostAsync($"/api/v1/forms/{id}/starts/{quitter}/progress", new { field_id = field });
        }

        var reply = await api.Admin.GetAsync($"/api/v1/forms/{id}/analytics");
        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        var stats = reply.Data;

        stats.GetProperty("starts").GetInt32().Should().Be(4);
        stats.GetProperty("submissions").GetInt32().Should().Be(1);
        stats.GetProperty("completion_rate").GetDouble().Should().Be(25);
        var worst = stats.GetProperty("drop_offs").EnumerateArray().First();
        worst.GetProperty("field_id").GetString().Should().Be("age");
        worst.GetProperty("count").GetInt32().Should().Be(2);
    }
}
