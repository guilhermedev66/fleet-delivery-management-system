using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace FleetDelivery.ArchitectureTests;

/// <summary>
/// Enforces the module-boundary rules from docs/ARCHITECTURE.md for the
/// Shipments module: Domain has no EF Core/ASP.NET Core references and
/// doesn't reach into Infrastructure; Application doesn't reach into
/// Infrastructure either (dependencies point inward, per the standard
/// Domain &lt;- Application &lt;- Infrastructure layering) — mirrors
/// <see cref="IdentityModuleBoundaryTests"/>.
/// </summary>
public class ShipmentsModuleBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(FleetDelivery.Modules.Shipments.Domain.Shipment).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(FleetDelivery.Modules.Shipments.Application.Shipments.CreateShipmentCommand).Assembly;

    private const string DomainNamespace = "FleetDelivery.Modules.Shipments.Domain";
    private const string ApplicationNamespace = "FleetDelivery.Modules.Shipments.Application";
    private const string InfrastructureNamespace = "FleetDelivery.Modules.Shipments.Infrastructure";
    private const string IdentityInfrastructureNamespace = "FleetDelivery.Modules.Identity.Infrastructure";
    private const string VehiclesInfrastructureNamespace = "FleetDelivery.Modules.Vehicles.Infrastructure";

    [Fact]
    public void Shipments_Domain_should_not_depend_on_Shipments_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Shipments_Domain_should_not_depend_on_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Shipments_Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Shipments_Application_should_not_depend_on_Shipments_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Shipments_Application_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    /// <summary>
    /// Cross-module rule: Shipments' Application layer is allowed to call
    /// Identity's Application public contract (MediatR's <c>GetCurrentUserQuery</c>,
    /// used by <c>AssignCommand</c> to validate a driverId) — but never
    /// reaches into Identity's Infrastructure/internals. Identity and
    /// Shipments can both depend on BuildingBlocks; they must never depend
    /// on each other's Infrastructure.
    /// </summary>
    [Fact]
    public void Shipments_should_not_depend_on_Identity_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(IdentityInfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));

        var domainResult = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(IdentityInfrastructureNamespace)
            .GetResult();

        domainResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(domainResult));
    }

    /// <summary>
    /// Same rule as <see cref="Shipments_should_not_depend_on_Identity_Infrastructure"/>,
    /// for Vehicles: Shipments' <c>AssignCommand</c> validates a vehicleId via
    /// Vehicles' Application public contract (<c>GetVehicleByIdQuery</c>) but
    /// must never reach into Vehicles' Infrastructure/internals.
    /// </summary>
    [Fact]
    public void Shipments_should_not_depend_on_Vehicles_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(VehiclesInfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));

        var domainResult = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(VehiclesInfrastructureNamespace)
            .GetResult();

        domainResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(domainResult));
    }

    /// <summary>The reverse direction: Vehicles never reaches into Shipments' Infrastructure/internals — the dependency only runs Shipments -> Vehicles, never back.</summary>
    [Fact]
    public void Vehicles_should_not_depend_on_Shipments_Infrastructure()
    {
        var vehiclesDomainAssembly = typeof(FleetDelivery.Modules.Vehicles.Domain.Vehicle).Assembly;
        var vehiclesApplicationAssembly = typeof(FleetDelivery.Modules.Vehicles.Application.Vehicles.RegisterVehicleCommand).Assembly;

        var domainResult = Types.InAssembly(vehiclesDomainAssembly)
            .That().ResideInNamespace("FleetDelivery.Modules.Vehicles.Domain")
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        domainResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(domainResult));

        var applicationResult = Types.InAssembly(vehiclesApplicationAssembly)
            .That().ResideInNamespace("FleetDelivery.Modules.Vehicles.Application")
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        applicationResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(applicationResult));
    }

    /// <summary>The reverse direction: Identity never reaches into Shipments' Infrastructure/internals either.</summary>
    [Fact]
    public void Identity_should_not_depend_on_Shipments_Infrastructure()
    {
        var identityDomainAssembly = typeof(FleetDelivery.Modules.Identity.Domain.User).Assembly;
        var identityApplicationAssembly = typeof(FleetDelivery.Modules.Identity.Application.Users.LoginCommand).Assembly;

        var domainResult = Types.InAssembly(identityDomainAssembly)
            .That().ResideInNamespace("FleetDelivery.Modules.Identity.Domain")
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        domainResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(domainResult));

        var applicationResult = Types.InAssembly(identityApplicationAssembly)
            .That().ResideInNamespace("FleetDelivery.Modules.Identity.Application")
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        applicationResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(applicationResult));
    }

    private static string BuildFailureMessage(TestResult result) =>
        result.FailingTypes is null
            ? "Rule failed."
            : "Rule violated by: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
