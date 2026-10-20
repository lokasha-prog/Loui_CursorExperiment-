using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ResourceTests(NmsApiFixture api)
{
    private static readonly byte[] Pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");

    private static byte[] Docx()
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using var w = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            w.Write("<w:document/>");
        }

        return stream.ToArray();
    }

    private async Task<Guid> FolderAsync(string audience)
    {
        var made = await api.Admin.PostAsync("/api/v1/resources/folders", new { name = new { en = $"Folder {Guid.NewGuid():N}"[..14], ar = "مجلد" }, audience });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        return made.Data.EnumerateArray().OrderByDescending(f => f.GetProperty("name").GetProperty("en").GetString()).First(f => f.GetProperty("audience").GetString() == audience).GetProperty("id").GetGuid();
    }

    private async Task<Guid> UploadAsync(Guid folder, string title, byte[]? file = null, string name = "handbook.pdf", string tags = "handbook")
    {
        var r = await api.Admin.PostFormAsync("/api/v1/resources/documents", new() { ["folder_id"] = folder.ToString(), ["title"] = title, ["tags"] = tags }, file ?? Pdf, name);
        r.Status.Should().Be(HttpStatusCode.Created, r.Raw);
        return r.Data.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Parents_see_only_parent_folders_and_their_documents_and_staff_folders_stay_hidden()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var open = await FolderAsync("parents");
        var closed = await FolderAsync("staff");
        var visible = await UploadAsync(open, "Parent handbook");
        var hidden = await UploadAsync(closed, "Staff policy");

        var folders = await parent.Client.GetAsync("/api/v1/resources/folders");
        var docs = await parent.Client.GetAsync("/api/v1/resources/documents?limit=100");
        var ok = await parent.Client.GetBytesAsync($"/api/v1/resources/documents/{visible}/download");
        var blocked = await parent.Client.GetBytesAsync($"/api/v1/resources/documents/{hidden}/download");
        var upload = await parent.Client.PostAsync("/api/v1/resources/folders", new { name = new { en = "Mine" }, audience = "parents" });

        folders.Data.EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).Should().Contain(open).And.NotContain(closed);
        docs.Items.Select(d => d.GetProperty("id").GetGuid()).Should().Contain(visible).And.NotContain(hidden);
        ok.Status.Should().Be(HttpStatusCode.OK);
        ok.ContentType.Should().Be("application/pdf");
        blocked.Status.Should().Be(HttpStatusCode.NotFound);
        upload.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_real_documents_are_accepted_by_what_they_are_not_by_their_name()
    {
        var folder = await FolderAsync("parents");
        var docx = await api.Admin.PostFormAsync("/api/v1/resources/documents", new() { ["folder_id"] = folder.ToString(), ["title"] = "Form" }, Docx(), "form.docx");
        var fake = await api.Admin.PostFormAsync("/api/v1/resources/documents", new() { ["folder_id"] = folder.ToString(), ["title"] = "Evil" }, System.Text.Encoding.ASCII.GetBytes("MZ this is a program"), "evil.pdf");
        var script = await api.Admin.PostFormAsync("/api/v1/resources/documents", new() { ["folder_id"] = folder.ToString(), ["title"] = "Page" }, System.Text.Encoding.ASCII.GetBytes("<html><script>1</script></html>"), "page.pdf");
        var noFolder = await api.Admin.PostFormAsync("/api/v1/resources/documents", new() { ["folder_id"] = Guid.NewGuid().ToString(), ["title"] = "Lost" }, Pdf, "x.pdf");

        docx.Status.Should().Be(HttpStatusCode.Created, docx.Raw);
        docx.Data.GetProperty("content_type").GetString().Should().Contain("wordprocessingml");
        fake.Status.Should().Be(HttpStatusCode.BadRequest);
        script.Status.Should().Be(HttpStatusCode.BadRequest);
        noFolder.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_new_version_keeps_the_old_one_parents_get_the_latest_and_every_download_is_logged()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var folder = await FolderAsync("parents");
        var doc = await UploadAsync(folder, "Fee schedule");
        var second = System.Text.Encoding.ASCII.GetBytes("%PDF-1.5\nsecond version\n%%EOF");

        var v2 = await api.Admin.PostFormAsync($"/api/v1/resources/documents/{doc}/versions", new(), second, "fees-2027.pdf");
        var versions = await api.Admin.GetAsync($"/api/v1/resources/documents/{doc}/versions");
        var parentGets = await parent.Client.GetBytesAsync($"/api/v1/resources/documents/{doc}/download?version=1");
        var staffOld = await api.Admin.GetBytesAsync($"/api/v1/resources/documents/{doc}/download?version=1");
        var log = await api.Admin.GetAsync($"/api/v1/resources/documents/{doc}/downloads");

        v2.Data.GetProperty("current_version").GetInt32().Should().Be(2);
        versions.Data.GetArrayLength().Should().Be(2);
        parentGets.Bytes.Should().Equal(second);
        staffOld.Bytes.Should().Equal(Pdf);
        log.Data.GetArrayLength().Should().Be(2);
        log.Data.EnumerateArray().Select(l => l.GetProperty("version").GetInt32()).Should().BeEquivalentTo([2, 1]);
    }

    [Fact]
    public async Task Search_finds_by_title_or_tag_and_folders_with_contents_cannot_be_removed()
    {
        var folder = await FolderAsync("parents");
        var marker = $"zebra{Guid.NewGuid():N}"[..12];
        var doc = await UploadAsync(folder, "Term dates", tags: marker);

        var byTag = await api.Admin.GetAsync($"/api/v1/resources/documents?search={marker}");
        var none = await api.Admin.GetAsync("/api/v1/resources/documents?search=qqqqnotthere");
        var removeFolder = await api.Admin.DeleteAsync($"/api/v1/resources/folders/{folder}");
        await api.Admin.DeleteAsync($"/api/v1/resources/documents/{doc}");
        var removeEmpty = await api.Admin.DeleteAsync($"/api/v1/resources/folders/{folder}");

        byTag.Items.Select(d => d.GetProperty("id").GetGuid()).Should().Contain(doc);
        none.Items.Should().BeEmpty();
        removeFolder.Status.Should().Be(HttpStatusCode.Conflict);
        removeEmpty.Status.Should().Be(HttpStatusCode.OK);
    }
}
