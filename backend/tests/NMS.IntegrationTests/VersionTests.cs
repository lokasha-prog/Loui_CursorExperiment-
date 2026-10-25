using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class VersionTests(NmsApiFixture api)
{
    [Fact]
    public async Task Anyone_can_see_which_version_is_running_and_it_is_the_one_in_the_VERSION_file()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "VERSION");

        var reply = await api.NewClient().GetAsync("/api/v1/version");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetProperty("version").GetString().Should().Be(File.ReadAllText(file).Trim());
    }
}
