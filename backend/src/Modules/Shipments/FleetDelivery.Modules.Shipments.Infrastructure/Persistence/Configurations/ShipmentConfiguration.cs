using FleetDelivery.Modules.Shipments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Shipment"/>. <see cref="Address"/> is mapped twice as an
/// EF owned type (Origin/Destination) into the same table with distinct
/// column prefixes; <see cref="TrackingEvent"/> and <see cref="DeliveryAttempt"/>
/// are owned collections in their own tables (own PK from
/// <c>Entity&lt;Guid&gt;.Id</c>, FK back to the owning shipment) — EF Core
/// loads owned collections automatically with their owner, no explicit
/// <c>Include</c> needed when querying a <see cref="Shipment"/>.
/// </summary>
public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments", table => table.HasCheckConstraint(
            "ck_shipments_status",
            $"status IN ({string.Join(", ", Enum.GetNames<ShipmentStatus>().Select(name => $"'{name}'"))})"));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.TrackingNumber)
            .HasColumnName("tracking_number")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(s => s.TrackingNumber)
            .IsUnique()
            .HasDatabaseName("ix_shipments_tracking_number");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.RecipientName)
            .HasColumnName("recipient_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.RecipientPhone)
            .HasColumnName("recipient_phone")
            .HasMaxLength(32)
            .IsRequired();

        builder.OwnsOne(s => s.OriginAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("origin_street").HasMaxLength(256).IsRequired();
            address.Property(a => a.City).HasColumnName("origin_city").HasMaxLength(128).IsRequired();
            address.Property(a => a.State).HasColumnName("origin_state").HasMaxLength(128).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("origin_postal_code").HasMaxLength(32).IsRequired();
            address.Property(a => a.Country).HasColumnName("origin_country").HasMaxLength(128).IsRequired();
        });

        builder.Navigation(s => s.OriginAddress).IsRequired();

        builder.OwnsOne(s => s.DestinationAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("destination_street").HasMaxLength(256).IsRequired();
            address.Property(a => a.City).HasColumnName("destination_city").HasMaxLength(128).IsRequired();
            address.Property(a => a.State).HasColumnName("destination_state").HasMaxLength(128).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("destination_postal_code").HasMaxLength(32).IsRequired();
            address.Property(a => a.Country).HasColumnName("destination_country").HasMaxLength(128).IsRequired();
        });

        builder.Navigation(s => s.DestinationAddress).IsRequired();

        builder.Property(s => s.AssignedDriverId)
            .HasColumnName("assigned_driver_id");

        builder.Property(s => s.AssignedVehicleId)
            .HasColumnName("assigned_vehicle_id");

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.HasProofOfDelivery)
            .HasColumnName("has_proof_of_delivery")
            .IsRequired();

        // Hand-rolled optimistic-concurrency token — see the class-level doc
        // comment on Shipment for why this isn't Postgres's xmin.
        builder.Property(s => s.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.OwnsMany(s => s.TrackingEvents, trackingEvent =>
        {
            trackingEvent.ToTable("tracking_events");
            trackingEvent.WithOwner().HasForeignKey(te => te.ShipmentId);
            trackingEvent.HasKey(te => te.Id);

            trackingEvent.Property(te => te.Id).HasColumnName("id").ValueGeneratedNever();
            trackingEvent.Property(te => te.ShipmentId).HasColumnName("shipment_id");
            trackingEvent.Property(te => te.Type).HasColumnName("type").HasMaxLength(64).IsRequired();
            trackingEvent.Property(te => te.OccurredAt).HasColumnName("occurred_at").IsRequired();
            trackingEvent.Property(te => te.ActorUserId).HasColumnName("actor_user_id");
            trackingEvent.Property(te => te.Notes).HasColumnName("notes");

            trackingEvent.HasIndex(te => te.ShipmentId).HasDatabaseName("ix_tracking_events_shipment_id");
        });

        builder.Navigation(s => s.TrackingEvents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(s => s.DeliveryAttempts, deliveryAttempt =>
        {
            deliveryAttempt.ToTable("delivery_attempts", table => table.HasCheckConstraint(
                "ck_delivery_attempts_outcome",
                $"outcome IN ({string.Join(", ", Enum.GetNames<DeliveryAttemptOutcome>().Select(name => $"'{name}'"))})"));
            deliveryAttempt.WithOwner().HasForeignKey(da => da.ShipmentId);
            deliveryAttempt.HasKey(da => da.Id);

            deliveryAttempt.Property(da => da.Id).HasColumnName("id").ValueGeneratedNever();
            deliveryAttempt.Property(da => da.ShipmentId).HasColumnName("shipment_id");
            deliveryAttempt.Property(da => da.DriverId).HasColumnName("driver_id").IsRequired();
            deliveryAttempt.Property(da => da.AttemptedAt).HasColumnName("attempted_at").IsRequired();
            deliveryAttempt.Property(da => da.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(32).IsRequired();
            deliveryAttempt.Property(da => da.Notes).HasColumnName("notes");

            deliveryAttempt.HasIndex(da => da.ShipmentId).HasDatabaseName("ix_delivery_attempts_shipment_id");
        });

        builder.Navigation(s => s.DeliveryAttempts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
