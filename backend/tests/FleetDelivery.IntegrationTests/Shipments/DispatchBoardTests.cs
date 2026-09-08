using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.Api.RealTime;
using FleetDelivery.BuildingBlocks.Messaging;
using FleetDelivery.IntegrationTests.Infrastructure;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Shipments.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FleetDelivery.IntegrationTests.Shipments;

/// <summary>
/// M5: proves the full pipeline end to end — create shipment -> M4 outbox
/// row -> published to RabbitMQ -> the real (always-on in this factory,
/// unlike <see cref="OutboxPublisherTests"/>'s manually-triggered publisher)
/// <c>DispatchBoardConsumerHostedService</c> consumes it -> broadcasts over
/// a real SignalR connection -> a connected, authenticated Dispatcher
/// actually receives it. Uses <see cref="HttpTransportType.LongPolling"/>
/// because <c>WebApplicationFactory</c>'s in-memory <c>TestServer</c>
/// doesn't support real WebSockets — the documented workaround for
/// SignalR-over-<c>TestServer</c>.
/// </summary>
public sealed class DispatchBoardTests : IClassFixture<OutboxPublisherApiFactory>
{
    private readonly OutboxPublisherApiFactory _factory;

    public DispatchBoardTests(OutboxPublisherApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task An_authenticated_Dispatcher_receives_a_real_broadcast_when_a_shipment_is_created()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/dispatch"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(dispatcherToken)!;
            })
            .Build();

        var receivedEvents = new System.Collections.Concurrent.ConcurrentQueue<DispatchBoardEvent>();
        connection.On<DispatchBoardEvent>("shipmentEvent", evt => receivedEvents.Enqueue(evt));

        await connection.StartAsync();

        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        // The consumer is really running in the background in this factory —
        // publish deterministically (as OutboxPublisherTests does) rather
        // than waiting out a poll interval, then wait for the async
        // consume-and-broadcast to actually reach this client.
        var dbContext = _factory.CreateShipmentsDbContext();
        var processor = new OutboxBatchProcessor(
            dbContext,
            _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IIntegrationEventPublisher>(),
            Options.Create(new OutboxPublisherOptions()),
            NullLogger<OutboxBatchProcessor>.Instance);
        await processor.ProcessBatchAsync();

        DispatchBoardEvent? matched = null;

        for (var attempt = 0; attempt < 40 && matched is null; attempt++)
        {
            matched = receivedEvents.FirstOrDefault(e =>
                e.Type == "ShipmentCreated" && e.Data.GetProperty("shipmentId").GetString() == shipment.Id.ToString());

            if (matched is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }

        matched.Should().NotBeNull("a Dispatcher connected to the dispatch hub must receive a real broadcast for a shipment it's authorized to see");

        await connection.StopAsync();
    }

    [Fact]
    public async Task An_authenticated_Driver_does_not_receive_dispatchers_group_broadcasts()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var (_, driverEmail, driverPassword) = await _factory.CreateUserAsync(Role.Driver);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);
        var driverToken = await LoginAsync(client, driverEmail, driverPassword);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/dispatch"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(driverToken)!;
            })
            .Build();

        var receivedAnyEvent = false;
        connection.On<DispatchBoardEvent>("shipmentEvent", _ => receivedAnyEvent = true);

        await connection.StartAsync();

        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        var dbContext = _factory.CreateShipmentsDbContext();
        var processor = new OutboxBatchProcessor(
            dbContext,
            _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IIntegrationEventPublisher>(),
            Options.Create(new OutboxPublisherOptions()),
            NullLogger<OutboxBatchProcessor>.Instance);
        await processor.ProcessBatchAsync();

        // Give the (correctly-scoped) broadcast a real chance to arrive before asserting its absence.
        await Task.Delay(TimeSpan.FromSeconds(2));

        receivedAnyEvent.Should().BeFalse("a Driver must never receive the dispatchers-group firehose — group membership is server-derived from role, not client-requested");
        shipment.Should().NotBeNull();

        await connection.StopAsync();
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
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ShipmentResponse>())!;
    }

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return body!.AccessToken;
    }

    private static void Authorize(HttpRequestMessage request, string accessToken) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
