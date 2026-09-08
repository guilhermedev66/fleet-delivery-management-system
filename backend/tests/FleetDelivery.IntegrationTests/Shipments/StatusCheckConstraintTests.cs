using FleetDelivery.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FleetDelivery.IntegrationTests.Shipments;

/// <summary>
/// Proves the Postgres <c>CHECK</c> constraints added alongside the enum
/// columns actually have teeth — not just that migrations applying them
/// succeeded, which every other integration test already demonstrates
/// implicitly. Bypasses EF Core (raw SQL) specifically because the whole
/// point of a DB-level constraint is protecting against writes that don't
/// go through the application layer at all.
/// </summary>
public sealed class StatusCheckConstraintTests : IClassFixture<ShipmentsApiFactory>
{
    private readonly ShipmentsApiFactory _factory;

    public StatusCheckConstraintTests(ShipmentsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task An_invalid_shipment_status_is_rejected_by_the_database_even_via_raw_SQL()
    {
        await using var dbContext = _factory.CreateShipmentsDbContext();

        var act = () => dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO shipments.shipments
                (id, tracking_number, status, recipient_name, recipient_phone,
                 origin_street, origin_city, origin_state, origin_postal_code, origin_country,
                 destination_street, destination_city, destination_state, destination_postal_code, destination_country,
                 created_by_user_id, created_at, version)
            VALUES
                (gen_random_uuid(), 'FD-CHECKTEST', 'NotARealStatus', 'Test', '555-0000',
                 'A', 'B', 'C', '00000', 'US',
                 'A', 'B', 'C', '00000', 'US',
                 gen_random_uuid(), now(), 0)
            """);

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23514", "23514 is Postgres's check-violation SQLSTATE")
            .Where(ex => ex.ConstraintName == "ck_shipments_status");
    }
}
