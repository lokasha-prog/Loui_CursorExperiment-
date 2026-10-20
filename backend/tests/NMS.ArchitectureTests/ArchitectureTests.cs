using FluentAssertions;
using NetArchTest.Rules;
using NMS.Domain.Common;
using Xunit;

namespace NMS.ArchitectureTests;

public class LayeringTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(Entity).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(NMS.Application.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_but_the_shared_kernel()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("NMS.Application", "NMS.Infrastructure", "NMS.Api", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_know_about_infrastructure_or_the_api()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("NMS.Infrastructure", "NMS.Api", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Every_persistent_entity_is_tenant_scoped()
    {
        var entities = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAuditable).IsAssignableFrom(t));

        entities.Should().NotBeEmpty();
        entities.Should().OnlyContain(t => typeof(ITenantEntity).IsAssignableFrom(t));
    }

    [Fact]
    public void Money_is_never_stored_as_floating_point()
    {
        var offenders = Domain.GetTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => (p.PropertyType == typeof(double) || p.PropertyType == typeof(float) || p.PropertyType == typeof(decimal))
                && (p.Name.Contains("Amount") || p.Name.Contains("Fils") || p.Name.Contains("Price") || p.Name.Contains("Salary")))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}");

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Every_command_and_query_handler_lives_in_the_application_layer()
    {
        var result = Types.InAssembly(Application).That().HaveNameEndingWith("Handler")
            .Should().ResideInNamespaceStartingWith("NMS.Application").GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
