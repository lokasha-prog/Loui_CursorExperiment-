using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PublicEnquiryTests(NmsApiFixture api)
{
    [Fact]
    public async Task A_family_can_send_an_enquiry_without_signing_in_and_staff_see_it_as_a_website_enquiry()
    {
        var anon = api.NewClient();
        var family = $"Online {Guid.NewGuid():N}"[..20];

        var form = await anon.GetAsync($"/api/v1/public/enquiry-form/{api.TenantSlug}");
        var sent = await anon.PostAsync($"/api/v1/public/enquiries/{api.TenantSlug}", new { family_name = family, child_name = "Noor", email = "online@test.local", message = "Do you have a place from January?" });
        var list = await api.Admin.GetAsync($"/api/v1/enquiries?search={Uri.EscapeDataString(family)}");

        form.Status.Should().Be(HttpStatusCode.OK, form.Raw);
        sent.Status.Should().Be(HttpStatusCode.OK, sent.Raw);
        var found = list.Items.Single();
        found.GetProperty("source").GetString().Should().Be("website");
        found.GetProperty("stage").GetString().Should().Be("no_stage");
        found.GetProperty("notes").GetString().Should().Contain("January");
    }

    [Fact]
    public async Task A_robot_filling_the_hidden_field_looks_like_it_worked_but_nothing_is_saved_and_bad_input_is_refused()
    {
        var anon = api.NewClient();
        var family = $"Robot {Guid.NewGuid():N}"[..20];

        var robot = await anon.PostAsync($"/api/v1/public/enquiries/{api.TenantSlug}", new { family_name = family, child_name = "X", phone = "1", website = "http://spam.example" });
        var nowhere = await anon.PostAsync("/api/v1/public/enquiries/no-such-centre", new { family_name = "A", child_name = "B", phone = "1" });
        var noContact = await anon.PostAsync($"/api/v1/public/enquiries/{api.TenantSlug}", new { family_name = "A", child_name = "B" });
        var list = await api.Admin.GetAsync($"/api/v1/enquiries?search={Uri.EscapeDataString(family)}");

        robot.Status.Should().Be(HttpStatusCode.OK);
        list.Items.Should().BeEmpty();
        nowhere.Status.Should().Be(HttpStatusCode.NotFound);
        noContact.Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
