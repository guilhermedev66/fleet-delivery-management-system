using FleetDelivery.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Configurations;

/// <summary>Maps the Shipments module's own copy of <see cref="OutboxMessage"/> — see the doc comment on <c>ShipmentsDbContext</c> for why it's not shared across module schemas yet.</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(m => m.OccurredOn)
            .HasColumnName("occurred_on")
            .IsRequired();

        builder.Property(m => m.ProcessedOn)
            .HasColumnName("processed_on");

        builder.Property(m => m.Error)
            .HasColumnName("error");

        builder.Property(m => m.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(m => m.NextAttemptOn)
            .HasColumnName("next_attempt_on");

        // Partial index: only unprocessed rows are ever queried by the
        // publisher, and that set stays small relative to the full history
        // an index over the whole table would otherwise have to cover.
        builder.HasIndex(m => new { m.OccurredOn, m.NextAttemptOn })
            .HasFilter("processed_on IS NULL")
            .HasDatabaseName("ix_outbox_messages_unprocessed");
    }
}
