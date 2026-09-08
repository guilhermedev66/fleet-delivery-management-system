using FleetDelivery.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetDelivery.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="User"/>.
///
/// Email uniqueness note: we chose a persisted <c>normalized_email</c>
/// column (uppercase-invariant) with a plain unique index, rather than the
/// Postgres <c>citext</c> extension. citext would let a plain unique index
/// on <c>email</c> itself be case-insensitive, but it requires
/// <c>CREATE EXTENSION citext</c> per-database (an extra migration/deploy
/// step and a Postgres-specific extension dependency); a normalized column
/// is portable, explicit, and needs nothing beyond a migration. Documented
/// again in backend/README.md.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasCheckConstraint(
            "ck_users_role",
            $"role IN ({string.Join(", ", Enum.GetNames<Role>().Select(name => $"'{name}'"))})"));

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_users_normalized_email");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
