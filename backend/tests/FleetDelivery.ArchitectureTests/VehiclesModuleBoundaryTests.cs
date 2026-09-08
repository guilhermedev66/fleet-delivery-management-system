using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace FleetDelivery.ArchitectureTests;

/// <summary>
/// Mirrors <see cref="ShipmentsModuleBoundaryTests"/> for the Vehicles
/// module. Unlike Shipments, Vehicles makes no cross-module Application
/// calls of its own (nothing in Vehicles needs Identity or Shipments) — the
/// dependency runs the other way, from Shipments into Vehicles, which is
/// exercised by <see cref="ShipmentsModuleBoundaryTests"/>-style rules added
/// alongside the Shipments/Identity ones once that call exists.
/// </summary>
public class VehiclesModuleBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(FleetDelivery.Modules.Vehicles.Domain.Vehicle).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(FleetDelivery.Modules.Vehicles.Application.Vehicles.RegisterVehicleCommand).Assembly;

    private const string DomainNamespace = "FleetDelivery.Modules.Vehicles.Domain";
    private const string ApplicationNamespace = "FleetDelivery.Modules.Vehicles.Application";
    private const string InfrastructureNamespace = "FleetDelivery.Modules.Vehicles.Infrastructure";

    [Fact]
    public void Vehicles_Domain_should_not_depend_on_Vehicles_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Vehicles_Domain_should_not_depend_on_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Vehicles_Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Vehicles_Application_should_not_depend_on_Vehicles_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    [Fact]
    public void Vehicles_Application_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    private static string BuildFailureMessage(TestResult result) =>
        result.FailingTypes is null
            ? "Rule failed."
            : "Rule violated by: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
