using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.IntegrationTests.Infrastructure;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Shipments.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FleetDelivery.IntegrationTests.Shipments;

/// <summary>
/// Exercises multipart upload and binary download through the real HTTP
/// pipeline, with real JWT authorization and real Postgres persistence.
/// </summary>
public sealed class ProofOfDeliveryEndpointsTests : IClassFixture<ShipmentsApiFactory>
{
    private static readonly byte[] JpegPhoto = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] PngPhoto = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03];

    private readonly ShipmentsApiFactory _factory;

    public ProofOfDeliveryEndpointsTests(ShipmentsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Delivered_shipment_photo_can_be_uploaded_by_its_driver_and_downloaded_by_authorized_viewers()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var (_, otherDriverEmail, otherDriverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var (_, adminEmail, adminPassword) = await _factory.CreateUserAsync(Role.Admin);

        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var otherDriverToken = await LoginAsync(client, otherDriverEmail, otherDriverPassword);
        var adminToken = await LoginAsync(client, adminEmail, adminPassword);
        var shipment = await CreateAndDeliverShipmentAsync(client, dispatcherToken, driverToken, driverId);

        using var uploadResponse = await UploadAsync(client, shipment.Id, driverToken, PngPhoto, "image/jpeg", "delivery.jpg");
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedShipment = await uploadResponse.Content.ReadFromJsonAsync<ShipmentResponse>();
        updatedShipment!.HasProofOfDelivery.Should().BeTrue();

        await AssertDownloadAsync(client, shipment.Id, dispatcherToken, PngPhoto, "image/png");
        await AssertDownloadAsync(client, shipment.Id, driverToken, PngPhoto, "image/png");
        await AssertDownloadAsync(client, shipment.Id, adminToken, PngPhoto, "image/png");

        using var unauthorizedDownload = new HttpRequestMessage(HttpMethod.Get, $"/api/shipments/{shipment.Id}/proof-of-delivery");
        Authorize(unauthorizedDownload, otherDriverToken);
        using var unauthorizedResponse = await client.SendAsync(unauthorizedDownload);
        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, "another driver must not learn whether the shipment or photo exists");
    }

    [Fact]
    public async Task Upload_before_delivery_returns_409()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var shipment = await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, driverId);

        using var response = await UploadAsync(client, shipment.Id, driverToken, JpegPhoto, "image/jpeg", "delivery.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("shipment-not-delivered");
    }

    [Fact]
    public async Task Upload_by_a_different_driver_returns_404()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (assignedDriverId, _, _) = await _factory.CreateUserAsync(Role.Driver);
        var (_, otherDriverEmail, otherDriverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var otherDriverToken = await LoginAsync(client, otherDriverEmail, otherDriverPassword);
        var shipment = await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, assignedDriverId);

        using var response = await UploadAsync(client, shipment.Id, otherDriverToken, JpegPhoto, "image/jpeg", "delivery.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "an IDOR attempt must not confirm the shipment exists");
    }

    [Fact]
    public async Task Oversized_photo_returns_400()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var shipment = await CreateAndDeliverShipmentAsync(client, dispatcherToken, driverToken, driverId);
        var oversizedPhoto = new byte[ProofOfDeliveryPhoto.MaxContentBytes + 1];
        JpegPhoto.CopyTo(oversizedPhoto, 0);

        using var response = await UploadAsync(client, shipment.Id, driverToken, oversizedPhoto, "image/jpeg", "delivery.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("proof-of-delivery-too-large");
    }

    [Fact]
    public async Task Non_image_bytes_masquerading_as_an_image_return_400()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var shipment = await CreateAndDeliverShipmentAsync(client, dispatcherToken, driverToken, driverId);
        var executableBytes = "MZ this is not an image"u8.ToArray();

        using var response = await UploadAsync(client, shipment.Id, driverToken, executableBytes, "image/png", "delivery.png");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("invalid-proof-of-delivery-content");
    }

    [Fact]
    public async Task Two_concurrent_uploads_for_the_same_shipment_never_both_succeed_and_neither_returns_500()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var shipment = await CreateAndDeliverShipmentAsync(client, dispatcherToken, driverToken, driverId);

        // Same shipment, same driver, two distinct photos racing each
        // other — proves the database-level backstop (ProofOfDeliveryPhotoAlreadyExistsException),
        // not just the in-memory HasProofOfDelivery fast path a single
        // sequential request would exercise.
        var responses = await Task.WhenAll(
            UploadAsync(client, shipment.Id, driverToken, PngPhoto, "image/png", "a.png"),
            UploadAsync(client, shipment.Id, driverToken, JpegPhoto, "image/jpeg", "b.jpg"));

        try
        {
            responses.Should().Contain(r => r.StatusCode == HttpStatusCode.OK);
            responses.Should().Contain(r => r.StatusCode == HttpStatusCode.Conflict);
            responses.Should().NotContain(r => (int)r.StatusCode >= 500, "a losing race must map to a clean 409, never an unhandled 500");

            var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            var problem = await conflictResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Type.Should().Contain("proof-of-delivery-already-attached");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Second_upload_returns_409_and_does_not_replace_the_original_photo()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (driverId, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);
        var shipment = await CreateAndDeliverShipmentAsync(client, dispatcherToken, driverToken, driverId);

        using var firstResponse = await UploadAsync(client, shipment.Id, driverToken, PngPhoto, "image/png", "first.png");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var secondResponse = await UploadAsync(client, shipment.Id, driverToken, JpegPhoto, "image/jpeg", "replacement.jpg");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.Should().Contain("proof-of-delivery-already-attached");

        await AssertDownloadAsync(client, shipment.Id, dispatcherToken, PngPhoto, "image/png");
    }

    private async Task<ShipmentResponse> CreateAndDeliverShipmentAsync(
        HttpClient client,
        string dispatcherToken,
        string driverToken,
        Guid driverId)
    {
        var shipment = await CreateReadyAndAssignedShipmentAsync(client, dispatcherToken, driverId);
        shipment = await PostAsync(client, $"/api/shipments/{shipment.Id}/pickup", driverToken, new ExpectedVersionRequest(shipment.Version));
        shipment = await PostAsync(client, $"/api/shipments/{shipment.Id}/in-transit", driverToken, new ExpectedVersionRequest(shipment.Version));
        shipment = await PostAsync(client, $"/api/shipments/{shipment.Id}/out-for-delivery", driverToken, new ExpectedVersionRequest(shipment.Version));
        shipment = await PostAsync(client, $"/api/shipments/{shipment.Id}/deliver", driverToken, new MarkDeliveredRequest(shipment.Version, null, "Photo follows"));

        return shipment;
    }

    private async Task<ShipmentResponse> CreateReadyAndAssignedShipmentAsync(HttpClient client, string dispatcherToken, Guid driverId)
    {
        var vehicleId = await RegisterVehicleAsync(client, dispatcherToken);
        var shipment = await CreateShipmentAsync(client, dispatcherToken);
        shipment = await PostAsync(client, $"/api/shipments/{shipment.Id}/ready-for-dispatch", dispatcherToken, new ExpectedVersionRequest(shipment.Version));
        shipment = await PostAsync(client, $"/api/shipments/{shipment.Id}/assign", dispatcherToken, new AssignRequest(driverId, vehicleId, shipment.Version));

        return shipment;
    }

    private static async Task<ShipmentResponse> CreateShipmentAsync(HttpClient client, string dispatcherToken)
    {
        var body = new CreateShipmentRequest(
            "Jane Recipient",
            "+1-555-0100",
            new AddressRequest("1 Warehouse Rd", "Origin City", "IL", "60000", "USA"),
            new AddressRequest("2 Customer Ave", "Destination City", "IL", "60001", "USA"));

        return await PostAsync(client, "/api/shipments", dispatcherToken, body);
    }

    private static async Task<Guid> RegisterVehicleAsync(HttpClient client, string dispatcherToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(new RegisterVehicleRequest($"P-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), "Van", 500m)),
        };
        Authorize(request, dispatcherToken);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!.Id;
    }

    private static async Task<ShipmentResponse> PostAsync<TRequest>(HttpClient client, string url, string accessToken, TRequest body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        Authorize(request, accessToken);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<ShipmentResponse>())!;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid shipmentId,
        string accessToken,
        byte[] bytes,
        string declaredContentType,
        string fileName)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(declaredContentType);
        multipart.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shipments/{shipmentId}/proof-of-delivery")
        {
            Content = multipart,
        };
        Authorize(request, accessToken);

        return await client.SendAsync(request);
    }

    private static async Task AssertDownloadAsync(
        HttpClient client,
        Guid shipmentId,
        string accessToken,
        byte[] expectedBytes,
        string expectedContentType)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/shipments/{shipmentId}/proof-of-delivery");
        Authorize(request, accessToken);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(expectedContentType);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(expectedBytes);
        response.Headers.TryGetValues("X-Content-Type-Options", out var nosniffValues).Should().BeTrue();
        nosniffValues!.Should().ContainSingle().Which.Should().Be("nosniff");
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private static void Authorize(HttpRequestMessage request, string accessToken) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
