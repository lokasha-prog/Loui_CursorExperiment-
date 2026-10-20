using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class IdCardTests(NmsApiFixture api)
{
    private static string Header(byte[] bytes) => System.Text.Encoding.ASCII.GetString(bytes, 0, 4);

    private static int Pages(byte[] bytes) => System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(bytes), "/Type\\s*/Page[^s]").Count;

    [Fact]
    public async Task Cards_for_several_children_come_out_as_one_pdf_with_a_page_each_in_both_sizes_and_languages()
    {
        var room = await api.CreateRoomAsync();
        var kids = new[] { await api.CreateStudentAsync(room), await api.CreateStudentAsync(room), await api.CreateStudentAsync(room) };

        var a6 = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = kids, template = "student-sunshine" });
        var small = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = kids, template = "student-ocean", size = "cr80", language = "ar" });
        var picked = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = kids, template = "student-forest", fields = new[] { "name", "barcode" }, primary = "#112233" });

        foreach (var pdf in new[] { a6, small, picked })
        {
            pdf.Status.Should().Be(HttpStatusCode.OK);
            pdf.ContentType.Should().Be("application/pdf");
            Header(pdf.Bytes).Should().Be("%PDF");
            Pages(pdf.Bytes).Should().Be(3);
        }
    }

    [Fact]
    public async Task Staff_cards_work_and_the_template_must_match_the_group()
    {
        var staff = await api.CreateStaffAsync();
        var templates = await api.Admin.GetAsync("/api/v1/tools/id-card-templates");
        var studentTemplates = await api.Admin.GetAsync("/api/v1/tools/id-card-templates?scope=student");

        var ok = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "staff", ids = new[] { staff }, template = "staff-modern" });
        var wrongGroup = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "staff", ids = new[] { staff }, template = "student-ocean" });

        templates.Data.GetArrayLength().Should().BeGreaterOrEqualTo(10);
        studentTemplates.Data.GetArrayLength().Should().Be(5);
        ok.Status.Should().Be(HttpStatusCode.OK);
        wrongGroup.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bad_requests_are_refused_and_only_people_with_the_tools_permission_can_print()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var kid = await api.CreateStudentAsync(await api.CreateRoomAsync());

        var none = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = Array.Empty<Guid>(), template = "student-sunshine" });
        var colour = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = new[] { kid }, template = "student-sunshine", primary = "red" });
        var field = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = new[] { kid }, template = "student-sunshine", fields = new[] { "salary" } });
        var unknown = await api.Admin.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = new[] { Guid.NewGuid() }, template = "student-sunshine" });
        var denied = await teacher.Client.PostBytesAsync("/api/v1/tools/id-cards", new { scope = "student", ids = new[] { kid }, template = "student-sunshine" });

        none.Status.Should().Be(HttpStatusCode.BadRequest);
        colour.Status.Should().Be(HttpStatusCode.BadRequest);
        field.Status.Should().Be(HttpStatusCode.BadRequest);
        unknown.Status.Should().Be(HttpStatusCode.NotFound);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }
}
