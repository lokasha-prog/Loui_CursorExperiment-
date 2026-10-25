using System.Net;
using System.Text.Json;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DataChatTests(NmsApiFixture api)
{
    private static readonly Random Rng = new();

    private async Task<Guid> ChildAsync(Guid room, string first, string gender)
    {
        var made = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new
            {
                first_name = first, last_name = $"Family{Guid.NewGuid():N}"[..14], date_of_birth = DateTime.UtcNow.AddMonths(-26).ToString("yyyy-MM-dd"),
                gender, room_id = room,
            },
            guardians = Array.Empty<object>(),
        });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        var id = made.Data.GetProperty("id").GetGuid();
        (await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" })).Status.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<HttpStatusCode> CheckInAsync(Guid child) =>
        (await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { person_type = "student", person_id = child })).Status;

    private async Task<(ApiReply Reply, List<string> Results)> AskAsync(ApiClient client, params (string Name, object Args)[] calls)
    {
        api.Ai.ToolCalls.Clear();
        api.Ai.ToolCalls.AddRange(calls.Select(c => (c.Name, JsonSerializer.Serialize(c.Args))));
        api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("Here is what I found.");
        try
        {
            var reply = await client.PostAsync("/api/v1/ai/chat", new { question = "How many?", language = "en", history = new[] { new { role = "user", content = "Hello" }, new { role = "assistant", content = "Hi" } } });
            return (reply, api.Ai.ToolResults.ToList());
        }
        finally
        {
            api.Ai.ToolCalls.Clear();
            api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("Attendance was steady.");
        }
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task It_counts_girls_present_today_in_a_room_exactly_and_never_hands_the_AI_a_child_name()
    {
        var roomName = $"Sunflowers {Guid.NewGuid():N}"[..20];
        var room = await api.CreateRoomAsync(roomName);
        var girls = new[] { await ChildAsync(room, "Zainabqx", "female"), await ChildAsync(room, "Mariamqx", "female"), await ChildAsync(room, "Huda qx", "female") };
        var boy = await ChildAsync(room, "Omarqx", "male");
        foreach (var child in girls.Take(2).Append(boy))
        {
            (await CheckInAsync(child)).Should().Be(HttpStatusCode.OK);
        }

        (await api.Admin.PostAsync("/api/v1/attendance/actions/mark-absent", new { student_id = girls[2] })).Status.Should().Be(HttpStatusCode.OK);
        var owner = (await api.CreateUserAsync("super_admin")).Client;

        var (reply, results) = await AskAsync(owner,
            ("count_children", new { room = roomName, gender = "girls", attendance = "present" }),
            ("count_children", new { room = roomName, group_by = "attendance" }),
            ("count_children", new { room = "No Such Room" }),
            ("centre_info", new { }));

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetProperty("answer").GetString().Should().Be("Here is what I found.");
        reply.Data.GetProperty("lookups").GetArrayLength().Should().Be(4);
        Parse(results[0]).GetProperty("count").GetInt32().Should().Be(2);
        var breakdown = Parse(results[1]).GetProperty("breakdown");
        breakdown.GetProperty("present").GetInt32().Should().Be(3);
        breakdown.GetProperty("absent").GetInt32().Should().Be(1);
        Parse(results[2]).GetProperty("error").GetString().Should().Contain("No Such Room");
        Parse(results[2]).GetProperty("valid").EnumerateArray().Select(v => v.GetString()).Should().Contain(roomName);
        results.Should().NotContain(r => r.Contains("qx"), "no child's name ever goes to the AI provider");
        api.Ai.LastMessages!.Select(m => m.Role).Should().Equal("user", "assistant", "user");
    }

    [Fact]
    public async Task It_counts_teachers_on_leave_on_a_day()
    {
        var day = DateTime.UtcNow.AddDays(Rng.Next(2000, 9000)).ToString("yyyy-MM-dd");
        var type = (await api.Admin.PostAsync("/api/v1/leave-types", new { name = new Dictionary<string, string> { ["en"] = $"Annual {Guid.NewGuid():N}" }, applies_to = "both" })).Data.GetProperty("id").GetGuid();
        var teacher = await api.CreateStaffAsync();
        await api.CreateUserAsync("teacher", staffId: teacher);
        var other = await api.CreateStaffAsync();
        foreach (var staff in new[] { teacher, other })
        {
            var leave = await api.Admin.PostAsync("/api/v1/leaves", new { person_type = "staff", person_id = staff, leave_type_id = type, start_date = day, end_date = day, reason = "Holiday" });
            leave.Status.Should().Be(HttpStatusCode.Created, leave.Raw);
            (await api.Admin.PostAsync($"/api/v1/leaves/{leave.Data.GetProperty("id").GetGuid()}/actions/approve", new { })).Status.Should().Be(HttpStatusCode.OK);
        }

        var owner = (await api.CreateUserAsync("super_admin")).Client;
        var (reply, results) = await AskAsync(owner,
            ("count_staff", new { date = day, role = "teacher", on_leave = true }),
            ("count_leave", new { date = day, person = "staff" }),
            ("count_staff", new { date = "not a date" }));

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        Parse(results[0]).GetProperty("count").GetInt32().Should().Be(1);
        Parse(results[1]).GetProperty("people").GetInt32().Should().Be(2);
        Parse(results[2]).GetProperty("error").GetString().Should().Contain("not a date");
    }

    [Fact]
    public async Task It_counts_learning_and_room_figures_by_room_area_type_and_status()
    {
        var code = $"fw-{Guid.NewGuid():N}"[..12];
        var fw = (await api.Admin.PostAsync("/api/v1/learning/frameworks", new { code, name = new { en = $"Framework {code}" } })).Data.GetProperty("id").GetGuid();
        var tree = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{fw}/import", new
        {
            areas = new[] { new { code = "cal", name = new { en = "Communication" }, sort_order = 1 }, new { code = "pd", name = new { en = "Physical" }, sort_order = 2 } },
            age_bands = new[] { new { code = "b1", name = new { en = "Toddlers" }, min_months = 8, max_months = 60 } },
            milestones = new[]
            {
                new { area = "cal", age_band = "b1", code = "m1", statement = new { en = "Says two words" } },
                new { area = "pd", age_band = "b1", code = "m2", statement = new { en = "Climbs steps" } },
            },
        });
        tree.Status.Should().Be(HttpStatusCode.OK, tree.Raw);
        var ms = tree.Data.GetProperty("milestones").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToArray();
        var cal = tree.Data.GetProperty("areas")[0].GetProperty("id").GetGuid();

        var roomName = $"Rainbows {Guid.NewGuid():N}"[..19];
        var room = await api.CreateRoomAsync(roomName, capacity: 10, ratio: 4);
        var girl = await ChildAsync(room, "Salmaqx", "female");
        var boy = await ChildAsync(room, "Yusufqx", "male");
        (await CheckInAsync(girl)).Should().Be(HttpStatusCode.OK);

        foreach (var (child, type) in new[] { (girl, "concern"), (girl, "concern"), (boy, "informal") })
        {
            var made = await api.Admin.PostAsync("/api/v1/learning/observations", new { student_id = child, type, framework_id = fw, area_id = cal, milestone_ids = new[] { ms[0] }, notes = new { en = "Seen today" } });
            made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        }

        await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Stories" }, framework_id = fw, area_id = cal, room_id = room, milestone_ids = new[] { ms[0] } });
        await api.Admin.PostAsync("/api/v1/learning/lessons", new { title = new { en = "Plan" }, framework_id = fw, is_template = true });
        var template = (await api.Admin.PostAsync("/api/v1/learning/assessment-templates", new { name = new { en = $"Check {code}" }, framework_id = fw, scoring_kind = "three_point", milestone_ids = ms })).Data.GetProperty("id").GetGuid();
        var run = (await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = template, student_id = girl, term = "Autumn" })).Data.GetProperty("id").GetGuid();
        await api.Admin.PutAsync($"/api/v1/learning/assessments/{run}/scores", new
        {
            scores = new[] { new { milestone_id = ms[0], score = 2, note = "" }, new { milestone_id = ms[1], score = 1, note = "" } },
            staff_summary = new { en = "x" }, parent_summary = new { en = "x" },
        });
        (await api.Admin.PostAsync($"/api/v1/learning/assessments/{run}/actions/complete")).Status.Should().Be(HttpStatusCode.OK);
        await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = template, student_id = boy, term = "Autumn" });

        var owner = (await api.CreateUserAsync("super_admin")).Client;
        var (reply, results) = await AskAsync(owner,
            ("count_observations", new { room = roomName, type = "concern" }),
            ("count_observations", new { room = roomName, group_by = "type" }),
            ("count_lessons", new { framework = code, group_by = "template" }),
            ("count_assessments", new { room = roomName, group_by = "status", include_scores = true }),
            ("curriculum", new { framework = code }),
            ("rooms", new { room = roomName }),
            ("count_observations", new { area = "No Such Area", framework = code }),
            ("count_observations", new { group_by = "colour" }));

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        Parse(results[0]).GetProperty("count").GetInt32().Should().Be(2);
        Parse(results[0]).GetProperty("children_observed").GetInt32().Should().Be(1);
        Parse(results[0]).GetProperty("active_children_with_none").GetInt32().Should().Be(1);
        Parse(results[1]).GetProperty("breakdown").GetProperty("concern").GetInt32().Should().Be(2);
        Parse(results[1]).GetProperty("breakdown").GetProperty("informal").GetInt32().Should().Be(1);
        Parse(results[2]).GetProperty("count").GetInt32().Should().Be(2);
        Parse(results[2]).GetProperty("breakdown").GetProperty("template").GetInt32().Should().Be(1);
        var assessments = Parse(results[3]);
        assessments.GetProperty("count").GetInt32().Should().Be(2);
        assessments.GetProperty("breakdown").GetProperty("completed").GetInt32().Should().Be(1);
        assessments.GetProperty("breakdown").GetProperty("draft").GetInt32().Should().Be(1);
        assessments.GetProperty("milestone_scores_by_area").GetProperty("Communication").GetProperty("secure").GetInt32().Should().Be(1);
        assessments.GetProperty("milestone_scores_by_area").GetProperty("Physical").GetProperty("emerging").GetInt32().Should().Be(1);
        var framework = Parse(results[4]).GetProperty("frameworks")[0];
        framework.GetProperty("milestones").GetInt32().Should().Be(2);
        framework.GetProperty("milestones_planned_in_lessons").GetInt32().Should().Be(1);
        framework.GetProperty("milestones_seen_in_observations").GetInt32().Should().Be(1);
        framework.GetProperty("lesson_templates").GetInt32().Should().Be(1);
        Parse(results[4]).GetProperty("milestones_by_area").GetProperty("Physical").GetInt32().Should().Be(1);
        var roomRow = Parse(results[5]).GetProperty("rooms")[0];
        roomRow.GetProperty("active_children").GetInt32().Should().Be(2);
        roomRow.GetProperty("children_present").GetInt32().Should().Be(1);
        roomRow.GetProperty("free_places").GetInt32().Should().Be(8);
        roomRow.GetProperty("staff_needed_for_ratio").GetInt32().Should().Be(1);
        Parse(results[6]).GetProperty("valid").EnumerateArray().Select(v => v.GetString()).Should().Contain("Communication");
        Parse(results[7]).GetProperty("error").GetString().Should().Contain("colour");
        results.Should().NotContain(r => r.Contains("qx"), "no child's name ever goes to the AI provider");
    }

    [Fact]
    public async Task Only_the_super_administrator_may_chat_and_a_missing_AI_key_is_said_plainly()
    {
        var manager = (await api.CreateUserAsync("centre_admin")).Client;
        var it = (await api.CreateUserAsync("it")).Client;
        var owner = (await api.CreateUserAsync("super_admin")).Client;

        (await manager.PostAsync("/api/v1/ai/chat", new { question = "How many children?", language = "en" })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await it.PostAsync("/api/v1/ai/chat", new { question = "How many children?", language = "en" })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PostAsync("/api/v1/ai/chat", new { question = "", language = "en" })).Status.Should().Be(HttpStatusCode.BadRequest);

        api.Ai.IsConfigured = false;
        try
        {
            var notSetUp = await owner.PostAsync("/api/v1/ai/chat", new { question = "How many children?", language = "en" });
            notSetUp.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
            notSetUp.Raw.Should().Contain("AI_NOT_SET_UP");
        }
        finally
        {
            api.Ai.IsConfigured = true;
        }
    }
}
