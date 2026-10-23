using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.Property(t => t.Nip).HasMaxLength(18).IsRequired();

        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Nip).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(t => t.UserId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
