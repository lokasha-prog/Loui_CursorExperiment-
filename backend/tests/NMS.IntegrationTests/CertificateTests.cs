using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CertificateTests(NmsApiFixture api)
{
    private static int Pages(byte[] bytes) => System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(bytes), "/Type\\s*/Page[^s]").Count;

    [Fact]
    public async Task Certificates_for_several_children_come_out_as_one_pdf_with_a_page_each_in_both_languages()
    {
        var room = await api.CreateRoomAsync();
        var kids = new[] { await api.CreateStudentAsync(room), await api.CreateStudentAsync(room) };
        var signatures = new[] { new { name = "Sara Ahmed", title = "Head teacher" }, new { name = "Omar Ali", title = "Manager" } };

        var en = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "achievement-gold", student_ids = kids, signatures });
        var ar = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "playful-none-but-valid", student_ids = kids });
        var arOk = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "star-student", student_ids = kids, language = "ar", title = "نجم الأسبوع", body = "تميّز {name} هذا الأسبوع." });

        en.Status.Should().Be(HttpStatusCode.OK);
        en.ContentType.Should().Be("application/pdf");
        System.Text.Encoding.ASCII.GetString(en.Bytes, 0, 4).Should().Be("%PDF");
        Pages(en.Bytes).Should().Be(2);
        ar.Status.Should().Be(HttpStatusCode.BadRequest);
        arOk.Status.Should().Be(HttpStatusCode.OK);
        Pages(arOk.Bytes).Should().Be(2);
    }

    [Fact]
    public async Task Numbers_continue_for_each_design_and_every_printed_certificate_is_on_record()
    {
        var kid = await api.CreateStudentAsync(await api.CreateRoomAsync());
        var other = await api.CreateStudentAsync(await api.CreateRoomAsync());

        await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = new[] { kid } });
        await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = new[] { kid, other }, title = "Kindest friend" });
        await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "sports-day", student_ids = new[] { kid } });
        var history = await api.Admin.GetAsync($"/api/v1/tools/certificates?student_id={kid}&limit=50");

        var numbers = history.Items.Select(i => i.GetProperty("number").GetString()!).ToList();
        var kind = numbers.Where(n => n.StartsWith("KIND-")).Select(n => int.Parse(n[5..])).OrderBy(n => n).ToList();
        kind.Should().HaveCount(2);
        kind[1].Should().BeGreaterThan(kind[0]);
        numbers.Should().Contain(n => n.StartsWith("SPORT-"));
        history.Items.Select(i => i.GetProperty("title").GetString()).Should().Contain("Kindest friend");
    }

    [Fact]
    public async Task Templates_are_listed_and_bad_requests_and_missing_rights_are_refused()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var kid = await api.CreateStudentAsync(await api.CreateRoomAsync());
        var templates = await api.Admin.GetAsync("/api/v1/tools/certificate-templates");

        var none = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = Array.Empty<Guid>() });
        var colour = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = new[] { kid }, primary = "blue" });
        var manySignatures = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = new[] { kid }, signatures = Enumerable.Range(0, 4).Select(i => new { name = $"S{i}", title = "T" }).ToArray() });
        var unknownKid = await api.Admin.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = new[] { Guid.NewGuid() } });
        var denied = await teacher.Client.PostBytesAsync("/api/v1/tools/certificates", new { template = "kindness", student_ids = new[] { kid } });

        templates.Data.GetArrayLength().Should().BeGreaterOrEqualTo(10);
        none.Status.Should().Be(HttpStatusCode.BadRequest);
        colour.Status.Should().Be(HttpStatusCode.BadRequest);
        manySignatures.Status.Should().Be(HttpStatusCode.BadRequest);
        unknownKid.Status.Should().Be(HttpStatusCode.NotFound);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }
}
