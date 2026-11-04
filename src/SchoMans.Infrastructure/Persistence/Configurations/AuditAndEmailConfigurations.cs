using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Audit;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(l => l.EntityName).HasMaxLength(AuditLog.EntityNameMaxLength).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);

        // Audit lookups: one kind of entity by time, and the history of one row.
        builder.HasIndex(l => new { l.EntityName, l.CreatedAt });
        builder.HasIndex(l => new { l.EntityId, l.CreatedAt });
    }
}

internal sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.Property(m => m.ToAddress).HasMaxLength(320).IsRequired();
        builder.Property(m => m.Subject).HasMaxLength(EmailMessage.SubjectMaxLength).IsRequired();
        builder.Property(m => m.Body).HasMaxLength(EmailMessage.BodyMaxLength).IsRequired();
        builder.Property(m => m.LinkUrl).HasMaxLength(EmailMessage.LinkMaxLength).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(EmailMessage.ErrorMaxLength);

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        // The sender job takes the pending ones, oldest first.
        builder.HasIndex(m => new { m.Status, m.CreatedAt });
    }
}
