using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.IntegrationTests.Infrastructure;
using FleetDelivery.Modules.Identity.Domain;
using FluentAssertions;

namespace FleetDelivery.IntegrationTests.Vehicles;

/// <summary>
/// End-to-end tests against the real <c>/api/vehicles/*</c> endpoints and a
/// real Postgres (via Testcontainers), reusing <see cref="ShipmentsApiFactory"/>
/// since it already boots the full <c>FleetDelivery.Api</c> host (all module
/// schemas, real JWTs via <c>/api/auth/login</c>) — nothing about it is
/// actually Shipments-specific.
/// </summary>
public sealed class VehicleEndpointsTests : IClassFixture<ShipmentsApiFactory>
{
    private readonly ShipmentsApiFactory _factory;

    public VehicleEndpointsTests(ShipmentsApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Dispatcher_can_register_a_vehicle_and_then_read_it_back_by_id_and_in_the_list()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        var vehicle = await RegisterVehicleAsync(client, dispatcherToken, PlateNumber());

        vehicle.Status.Should().Be("Active");
        vehicle.Type.Should().Be("Van");

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/vehicles/{vehicle.Id}");
        Authorize(getRequest, dispatcherToken);
        var getResponse = await client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await getResponse.Content.ReadFromJsonAsync<VehicleResponse>())!.PlateNumber.Should().Be(vehicle.PlateNumber);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles?status=Active");
        Authorize(listRequest, dispatcherToken);
        var listResponse = await client.SendAsync(listRequest);
        var list = await listResponse.Content.ReadFromJsonAsync<VehicleListResponse>();
        list!.Items.Select(i => i.Id).Should().Contain(vehicle.Id);
    }

    [Fact]
    public async Task Driver_cannot_register_a_vehicle()
    {
        var client = CreateClient();
        var (_, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(new RegisterVehicleRequest(PlateNumber(), "Van", 500m)),
        };
        Authorize(request, driverToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registering_a_duplicate_plate_number_returns_409()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var plateNumber = PlateNumber();

        await RegisterVehicleAsync(client, dispatcherToken, plateNumber);

        // Same plate number, different case/whitespace — still a duplicate
        // once normalized, exactly as a real dispatcher retyping it would be.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(new RegisterVehicleRequest($" {plateNumber.ToLowerInvariant()} ", "Truck", 800m)),
        };
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Getting_an_unknown_vehicle_id_returns_404()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/vehicles/{Guid.NewGuid()}");
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string PlateNumber() => $"T-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<VehicleResponse> RegisterVehicleAsync(HttpClient client, string dispatcherToken, string plateNumber)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(new RegisterVehicleRequest(plateNumber, "Van", 500m)),
        };
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return body!.AccessToken;
    }

    private static void Authorize(HttpRequestMessage request, string accessToken) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
