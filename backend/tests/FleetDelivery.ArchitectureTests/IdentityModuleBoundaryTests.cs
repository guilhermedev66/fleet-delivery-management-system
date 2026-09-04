using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace FleetDelivery.ArchitectureTests;

/// <summary>
/// Enforces the module-boundary rules from docs/ARCHITECTURE.md for the
/// Identity module: Domain has no EF Core/ASP.NET Core references and
/// doesn't reach into Infrastructure; Application doesn't reach into
/// Infrastructure either (dependencies point inward, per the standard
/// Domain &lt;- Application &lt;- Infrastructure layering).
/// </summary>
public class IdentityModuleBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(FleetDelivery.Modules.Identity.Domain.User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(FleetDelivery.Modules.Identity.Application.Users.LoginCommand).Assembly;

    private const string DomainNamespace = "FleetDelivery.Modules.Identity.Domain";
    private const string ApplicationNamespace = "FleetDelivery.Modules.Identity.Application";
    private const string InfrastructureNamespace = "FleetDelivery.Modules.Identity.Infrastructure";

    [Fact]
    public void Identity_Domain_should_not_depend_on_Identity_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Identity_Domain_should_not_depend_on_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Identity_Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Identity_Application_should_not_depend_on_Identity_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    private static string BuildFailureMessage(TestResult result) =>
        result.FailingTypes is null
            ? "Rule failed."
            : "Rule violated by: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
