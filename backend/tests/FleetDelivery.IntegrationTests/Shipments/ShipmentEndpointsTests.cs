using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.IntegrationTests.Infrastructure;
using FleetDelivery.Modules.Identity.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetDelivery.IntegrationTests.Shipments;

/// <summary>
/// End-to-end tests against the real <c>/api/shipments/*</c> endpoints, a
/// real Postgres (via Testcontainers), and real JWTs minted by the real
/// <c>/api/auth/login</c> endpoint for Dispatcher and Driver users seeded
/// directly via <see cref="ShipmentsApiFactory.CreateUserAsync"/>.
///
/// NOTE: requires a Docker daemon. See <see cref="ShipmentsApiFactory"/>.
/// </summary>
public sealed class ShipmentEndpointsTests : IClassFixture<ShipmentsApiFactory>
{
    private readonly ShipmentsApiFactory _factory;

    public ShipmentEndpointsTests(ShipmentsApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Full_lifecycle_from_create_through_delivery_succeeds_via_real_HTTP_endpoints_with_real_JWTs()
    {
        var client = CreateClient();

        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);

        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var vehicleId = await RegisterVehicleAsync(client, dispatcherToken);

        var shipment = await CreateShipmentAsync(client, dispatcherToken);
        shipment.Status.Should().Be("Draft");
        shipment.Version.Should().Be(0);

        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/ready-for-dispatch", dispatcherToken, new ExpectedVersionRequest(shipment.Version)))!;
        shipment.Status.Should().Be("ReadyForDispatch");

        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/assign", dispatcherToken, new AssignRequest(driverId, vehicleId, shipment.Version)))!;
        shipment.Status.Should().Be("Assigned");
        shipment.AssignedDriverId.Should().Be(driverId);
        shipment.AssignedVehicleId.Should().Be(vehicleId);

        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/pickup", driverToken, new ExpectedVersionRequest(shipment.Version)))!;
        shipment.Status.Should().Be("PickedUp");

        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/in-transit", driverToken, new ExpectedVersionRequest(shipment.Version)))!;
        shipment.Status.Should().Be("InTransit");

        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/out-for-delivery", driverToken, new ExpectedVersionRequest(shipment.Version)))!;
        shipment.Status.Should().Be("OutForDelivery");

        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/deliver", driverToken, new MarkDeliveredRequest(shipment.Version, "Jane Recipient", "Left at door")))!;
        shipment.Status.Should().Be("Delivered");

        using var timelineRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/shipments/{shipment.Id}/timeline");
        Authorize(timelineRequest, dispatcherToken);
        var timelineResponse = await client.SendAsync(timelineRequest);
        timelineResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var timeline = await timelineResponse.Content.ReadFromJsonAsync<ShipmentTimelineResponse>();
        timeline!.Events.Select(e => e.Type).Should().Equal(
            "Created", "ReadyForDispatch", "Assigned", "PickedUp", "InTransit", "OutForDelivery", "Delivered");
    }

    [Fact]
    public async Task Driver_acting_on_a_shipment_not_assigned_to_them_gets_404_not_403()
    {
        var client = CreateClient();

        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (assignedDriverId, _, _) = await _factory.CreateUserAsync(Role.Driver);
        var (_, otherDriverEmail, otherDriverPassword) = await _factory.CreateUserAsync(Role.Driver);

        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var otherDriverToken = await LoginAsync(client, otherDriverEmail, otherDriverPassword);

        var shipment = await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, assignedDriverId);

        // The OTHER driver (not the one this shipment is assigned to) tries to act on it.
        using var pickupRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/shipments/{shipment.Id}/pickup")
        {
            Content = JsonContent.Create(new ExpectedVersionRequest(shipment.Version)),
        };
        Authorize(pickupRequest, otherDriverToken);
        var pickupResponse = await client.SendAsync(pickupRequest);
        pickupResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, "an IDOR attempt must not confirm the shipment exists");

        // Same driver also can't see it via GET — same 404, not a 403 that
        // would confirm the id exists.
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/shipments/{shipment.Id}");
        Authorize(getRequest, otherDriverToken);
        var getResponse = await client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Driver_list_is_always_server_side_filtered_to_their_own_shipments()
    {
        var client = CreateClient();

        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverAId, driverAEmail, driverAPassword) = await _factory.CreateUserAsync(Role.Driver);
        var (driverBId, _, _) = await _factory.CreateUserAsync(Role.Driver);

        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverAToken = await LoginAsync(client, driverAEmail, driverAPassword);

        var shipmentForA = await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, driverAId);
        var shipmentForB = await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, driverBId);

        // Driver A explicitly tries to pass driverId=driverB's id as a filter — must be ignored.
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/shipments?driverId={driverBId}");
        Authorize(listRequest, driverAToken);
        var listResponse = await client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await listResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();
        page!.Items.Select(i => i.Id).Should().Contain(shipmentForA.Id);
        page.Items.Select(i => i.Id).Should().NotContain(shipmentForB.Id);
    }

    [Fact]
    public async Task Stale_expectedVersion_returns_409_with_a_concurrency_conflict_error_type()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        // First call with the correct version succeeds and bumps the version...
        var afterFirst = await PostAsync(client, $"/api/shipments/{shipment.Id}/ready-for-dispatch", dispatcherToken, new ExpectedVersionRequest(shipment.Version));
        afterFirst!.Version.Should().BeGreaterThan(shipment.Version);

        // ...so replaying the now-stale (original) expectedVersion must 409.
        using var staleRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/shipments/{shipment.Id}/ready-for-dispatch")
        {
            Content = JsonContent.Create(new ExpectedVersionRequest(shipment.Version)),
        };
        Authorize(staleRequest, dispatcherToken);
        var staleResponse = await client.SendAsync(staleRequest);

        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var staleProblem = await staleResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        staleProblem!.Type.Should().Contain("concurrency-conflict");
    }

    [Fact]
    public async Task Invalid_transition_returns_409_with_a_different_error_type_than_concurrency_conflict()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        // A freshly-created shipment is in Draft — "reschedule" requires DeliveryFailed, so this is a genuinely invalid transition (not a version problem).
        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shipments/{shipment.Id}/reschedule")
        {
            Content = JsonContent.Create(new ExpectedVersionRequest(shipment.Version)),
        };
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("invalid-transition");
        problem.Type.Should().NotContain("concurrency-conflict");
    }

    [Fact]
    public async Task Assigning_a_nonexistent_vehicle_returns_400_with_an_invalid_vehicle_error_type()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, _, _) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        var shipment = await CreateShipmentAsync(client, dispatcherToken);
        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/ready-for-dispatch", dispatcherToken, new ExpectedVersionRequest(shipment.Version)))!;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shipments/{shipment.Id}/assign")
        {
            Content = JsonContent.Create(new AssignRequest(driverId, Guid.NewGuid(), shipment.Version)),
        };
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("invalid-vehicle");
    }

    [Fact]
    public async Task Available_drivers_list_marks_a_driver_unavailable_once_assigned_to_an_in_progress_shipment()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (busyDriverId, _, _) = await _factory.CreateUserAsync(Role.Driver);
        var (freeDriverId, _, _) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, busyDriverId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/shipments/drivers");
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var drivers = await response.Content.ReadFromJsonAsync<List<AvailableDriverResponse>>();
        drivers!.Should().Contain(d => d.Id == busyDriverId && !d.IsAvailable);
        drivers!.Should().Contain(d => d.Id == freeDriverId && d.IsAvailable);
    }

    [Fact]
    public async Task A_transition_writes_an_outbox_row_in_the_same_call_as_the_state_change()
    {
        var client = CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        await using var dbContext = _factory.CreateShipmentsDbContext();
        var outboxMessages = await dbContext.OutboxMessages
            .Where(m => m.Type == "ShipmentCreated" && m.Content.Contains(shipment.Id.ToString()))
            .ToListAsync();

        // Scoped to this shipment's id: the test class shares one database
        // across many [Fact]s, each of which creates its own shipment (and
        // therefore its own "ShipmentCreated" row), so an unscoped filter on
        // Type alone would pick up every other test's rows too.
        outboxMessages.Should().ContainSingle(m => m.ProcessedOn == null, "the row must exist, unpublished, from the very transaction that created the shipment");
    }

    private async Task<ShipmentResponse> CreateShipmentAsync(HttpClient client, string dispatcherToken)
    {
        var request = new CreateShipmentRequest(
            "Jane Recipient",
            "+1-555-0100",
            new AddressRequest("1 Warehouse Rd", "Origin City", "IL", "60000", "USA"),
            new AddressRequest("2 Customer Ave", "Destination City", "IL", "60001", "USA"));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/shipments") { Content = JsonContent.Create(request) };
        Authorize(httpRequest, dispatcherToken);
        var response = await client.SendAsync(httpRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<ShipmentResponse>())!;
    }

    private async Task<ShipmentResponse> CreateReadyAndAssignedShipmentAsync(HttpClient client, string dispatcherToken, Guid driverId)
    {
        var vehicleId = await RegisterVehicleAsync(client, dispatcherToken);

        var shipment = await CreateShipmentAsync(client, dispatcherToken);
        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/ready-for-dispatch", dispatcherToken, new ExpectedVersionRequest(shipment.Version)))!;
        shipment = (await PostAsync(client, $"/api/shipments/{shipment.Id}/assign", dispatcherToken, new AssignRequest(driverId, vehicleId, shipment.Version)))!;

        return shipment;
    }

    /// <summary>Registers an Active vehicle via the real <c>/api/vehicles</c> endpoint and returns its id, for tests that need a valid <c>vehicleId</c> to assign.</summary>
    private static async Task<Guid> RegisterVehicleAsync(HttpClient client, string dispatcherToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(new RegisterVehicleRequest($"T-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), "Van", 500m)),
        };
        Authorize(request, dispatcherToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!.Id;
    }

    private static async Task<ShipmentResponse?> PostAsync<TRequest>(HttpClient client, string url, string accessToken, TRequest body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        Authorize(request, accessToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<ShipmentResponse>();
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
