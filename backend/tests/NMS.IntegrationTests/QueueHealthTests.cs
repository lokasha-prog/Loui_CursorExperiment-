using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class QueueHealthTests(NmsApiFixture api)
{
    [Fact]
    public async Task Managers_can_see_how_many_alerts_and_reports_are_waiting_and_staff_cannot()
    {
        var reply = await api.Admin.GetAsync("/api/v1/system/queues");
        var teacher = await api.CreateUserAsync("teacher");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        foreach (var key in new[] { "alerts_waiting", "oldest_alert_waiting_minutes", "alerts_failed_last24_hours", "reports_waiting", "media_files", "media_megabytes" })
        {
            reply.Data.TryGetProperty(key, out _).Should().BeTrue(key);
        }

        (await teacher.Client.GetAsync("/api/v1/system/queues")).Status.Should().Be(HttpStatusCode.Forbidden);
    }
}
