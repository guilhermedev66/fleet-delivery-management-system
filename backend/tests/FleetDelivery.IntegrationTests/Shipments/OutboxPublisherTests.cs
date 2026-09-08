using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.BuildingBlocks.Messaging;
using FleetDelivery.IntegrationTests.Infrastructure;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Shipments.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FleetDelivery.IntegrationTests.Shipments;

/// <summary>
/// M4: real Postgres + real (Testcontainers) RabbitMQ end to end for the
/// happy path, plus deterministic, DI-independent coverage of the retry and
/// concurrent-claiming guarantees using hand-built <see cref="OutboxBatchProcessor"/>
/// instances (never the hosted service, which is disabled in this factory —
/// see <see cref="OutboxPublisherApiFactory"/>).
/// </summary>
public sealed class OutboxPublisherTests : IClassFixture<OutboxPublisherApiFactory>
{
    private readonly OutboxPublisherApiFactory _factory;

    public OutboxPublisherTests(OutboxPublisherApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Creating_a_shipment_writes_an_outbox_row_that_the_publisher_delivers_to_a_real_RabbitMQ_queue()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        await using var subscriberConnection = await new ConnectionFactory { Uri = new Uri(_factory.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var subscriberChannel = await subscriberConnection.CreateChannelAsync();
        await subscriberChannel.ExchangeDeclareAsync("fleet.events", ExchangeType.Topic, durable: true);
        var queueName = (await subscriberChannel.QueueDeclareAsync(exclusive: true)).QueueName;
        await subscriberChannel.QueueBindAsync(queueName, "fleet.events", "shipment.created");

        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        var processor = CreateBatchProcessor(publisher: null); // real, DI-resolved RabbitMqIntegrationEventPublisher
        var result = await processor.ProcessBatchAsync();
        result.Published.Should().BeGreaterThanOrEqualTo(1);

        var delivered = await PollForMessageAsync(subscriberChannel, queueName);
        delivered.Should().NotBeNull("the publisher must actually deliver to the exchange with the documented routing key");

        var envelope = JsonSerializer.Deserialize<JsonElement>(delivered!.Body.Span);
        envelope.GetProperty("type").GetString().Should().Be("ShipmentCreated");
        envelope.GetProperty("data").GetProperty("shipmentId").GetString().Should().Be(shipment.Id.ToString());

        await using var dbContext = _factory.CreateShipmentsDbContext();
        var row = dbContext.OutboxMessages.Single(m => m.Type == "ShipmentCreated" && m.Content.Contains(shipment.Id.ToString()));
        row.ProcessedOn.Should().NotBeNull();
        row.Error.Should().BeNull();
    }

    [Fact]
    public async Task A_failed_publish_leaves_the_row_unprocessed_with_a_backoff_and_does_not_lose_it()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        var shipment = await CreateShipmentAsync(client, dispatcherToken);

        var alwaysFails = new AlwaysFailsPublisher();
        var processor = CreateBatchProcessor(alwaysFails);

        var result = await processor.ProcessBatchAsync();
        result.Failed.Should().BeGreaterThanOrEqualTo(1);

        await using var dbContext = _factory.CreateShipmentsDbContext();
        var row = await dbContext.OutboxMessages.SingleAsync(m => m.Type == "ShipmentCreated" && m.Content.Contains(shipment.Id.ToString()));
        row.ProcessedOn.Should().BeNull("a failed publish must never be marked processed");
        row.Error.Should().NotBeNullOrEmpty();
        row.AttemptCount.Should().Be(1);
        row.NextAttemptOn.Should().NotBeNull().And.BeAfter(DateTimeOffset.UtcNow, "the row must be backed off, not immediately reclaimable — otherwise a persistently broken row would hot-loop");

        // Immediately reprocessing must NOT re-claim it — it's not due yet.
        var immediateRetry = await CreateBatchProcessor(alwaysFails).ProcessBatchAsync();
        immediateRetry.Claimed.Should().Be(0, "a row backed off into the future must not be claimed again before its NextAttemptOn");
    }

    [Fact]
    public async Task Two_concurrent_batch_processors_never_publish_the_same_row_twice()
    {
        var client = _factory.CreateClient();
        var (_, dispatcherEmail, dispatcherPassword) = await _factory.CreateUserAsync(Role.Dispatcher);
        var dispatcherToken = await LoginAsync(client, dispatcherEmail, dispatcherPassword);

        const int shipmentCount = 8;

        for (var i = 0; i < shipmentCount; i++)
        {
            await CreateShipmentAsync(client, dispatcherToken);
        }

        var countingPublisher = new CountingPublisher();

        // Each processor gets its own DbContext (its own connection/transaction) —
        // exactly the shape of two app instances racing the same poll.
        var first = CreateBatchProcessor(countingPublisher);
        var second = CreateBatchProcessor(countingPublisher);

        var results = await Task.WhenAll(first.ProcessBatchAsync(), second.ProcessBatchAsync());

        var totalClaimed = results.Sum(r => r.Claimed);
        totalClaimed.Should().BeGreaterThanOrEqualTo(shipmentCount, "every row created by this test must eventually be claimed by one of the two processors");

        // The real assertion: no messageId was ever handed to the publisher
        // more than once, across BOTH processors — proof FOR UPDATE SKIP
        // LOCKED prevented a double-claim, not just a lucky non-overlap.
        countingPublisher.PublishedMessageIds.Should().OnlyHaveUniqueItems();
    }

    private OutboxBatchProcessor CreateBatchProcessor(IIntegrationEventPublisher? publisher)
    {
        var dbContext = _factory.CreateShipmentsDbContext();
        var options = Options.Create(new OutboxPublisherOptions { BatchSize = 50, MaxBackoffSeconds = 300 });
        var effectivePublisher = publisher ?? _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        return new OutboxBatchProcessor(dbContext, effectivePublisher, options, NullLogger<OutboxBatchProcessor>.Instance);
    }

    private static async Task<BasicGetResult?> PollForMessageAsync(IChannel channel, string queueName)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var message = await channel.BasicGetAsync(queueName, autoAck: true);

            if (message is not null)
            {
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return null;
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

    private sealed class AlwaysFailsPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(string routingKey, OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated broker outage.");
    }

    private sealed class CountingPublisher : IIntegrationEventPublisher
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<Guid> _publishedMessageIds = [];

        public IReadOnlyCollection<Guid> PublishedMessageIds => _publishedMessageIds;

        public Task PublishAsync(string routingKey, OutboxMessage message, CancellationToken cancellationToken = default)
        {
            _publishedMessageIds.Add(message.Id);

            return Task.CompletedTask;
        }
    }
}
