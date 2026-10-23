using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.KeycloakId).HasMaxLength(64);
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(u => u.PhotoKey).HasMaxLength(512);

        // Unique among live rows only, so a soft-deleted account does not block its email or Keycloak id.
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(u => u.KeycloakId).IsUnique().HasFilter("[IsDeleted] = 0 AND [KeycloakId] IS NOT NULL");
    }
}
