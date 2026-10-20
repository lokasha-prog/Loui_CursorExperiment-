using System.Net;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NMS.Api.Endpoints;
using NMS.Application.Auth;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DevToolsTests(NmsApiFixture api)
{
    [Fact]
    public async Task The_helper_does_not_exist_outside_development()
    {
        var list = await api.NewClient().GetAsync("/api/v1/dev/test-users");
        var login = await api.NewClient().PostAsync("/api/v1/dev/quick-login", new { role = "teacher" });

        list.Status.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        login.Status.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void It_needs_both_the_development_environment_and_the_local_switch()
    {
        var on = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevTools:Enabled"] = "true" }).Build();
        var off = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        IWebHostEnvironment Env(string name) => new FakeEnvironment(name);

        DevToolsEndpoints.IsAvailable(Env("Development"), on).Should().BeTrue();
        DevToolsEndpoints.IsAvailable(Env("Development"), off).Should().BeFalse("the switch lives in local secrets only");
        DevToolsEndpoints.IsAvailable(Env("Production"), on).Should().BeFalse();
    }

    [Fact]
    public async Task Test_users_are_created_on_demand_with_only_their_roles_and_never_a_super_admin()
    {
        using var scope = api.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var teacher = await sender.Send(new DevQuickLoginCommand("teacher", null));
        var again = await sender.Send(new DevQuickLoginCommand("teacher", null));
        var super = await sender.Send(new DevQuickLoginCommand("super_admin", null));

        teacher.IsSuccess.Should().BeTrue();
        teacher.Value.Me.Roles.Should().Equal("teacher");
        teacher.Value.Me.Permissions.Should().NotContain("user_management.manage");
        again.Value.Me.Id.Should().Be(teacher.Value.Me.Id, "the same test user is reused");
        super.IsFailure.Should().BeTrue();
    }

    private sealed class FakeEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "t";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
