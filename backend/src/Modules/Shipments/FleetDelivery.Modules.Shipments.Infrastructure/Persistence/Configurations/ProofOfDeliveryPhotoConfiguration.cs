using FleetDelivery.Modules.Shipments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the photo as a separate entity so EF never pulls its potentially
/// multi-megabyte payload into ordinary shipment queries. Its primary key is
/// also a same-schema foreign key to the shipment, enforcing the one-photo
/// maximum and preventing orphaned evidence at the database layer.
/// </summary>
public sealed class ProofOfDeliveryPhotoConfiguration : IEntityTypeConfiguration<ProofOfDeliveryPhoto>
{
    public void Configure(EntityTypeBuilder<ProofOfDeliveryPhoto> builder)
    {
        builder.ToTable("proof_of_delivery_photos");

        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.Id)
            .HasColumnName("shipment_id")
            .ValueGeneratedNever();

        builder.Ignore(photo => photo.ShipmentId);

        builder.Property(photo => photo.Content)
            .HasColumnName("content")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(photo => photo.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(photo => photo.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(photo => photo.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        builder.HasOne<Shipment>()
            .WithOne()
            .HasForeignKey<ProofOfDeliveryPhoto>(photo => photo.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
