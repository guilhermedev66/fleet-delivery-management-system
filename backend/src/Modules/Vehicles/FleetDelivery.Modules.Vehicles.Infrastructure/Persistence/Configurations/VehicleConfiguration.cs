using FleetDelivery.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetDelivery.Modules.Vehicles.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", table =>
        {
            table.HasCheckConstraint(
                "ck_vehicles_type",
                $"type IN ({string.Join(", ", Enum.GetNames<VehicleType>().Select(name => $"'{name}'"))})");
            table.HasCheckConstraint(
                "ck_vehicles_status",
                $"status IN ({string.Join(", ", Enum.GetNames<VehicleStatus>().Select(name => $"'{name}'"))})");
        });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(v => v.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(20)
            .IsRequired();

        // The real domain invariant (plate numbers are physically unique);
        // IVehicleRepository.ExistsByPlateNumberAsync is only a friendly
        // fast-path in front of it. See VehiclesDbContext for the exception
        // translation on a race that slips past the fast-path.
        builder.HasIndex(v => v.PlateNumber)
            .IsUnique()
            .HasDatabaseName("ix_vehicles_plate_number");

        builder.Property(v => v.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(v => v.CapacityKg)
            .HasColumnName("capacity_kg")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
