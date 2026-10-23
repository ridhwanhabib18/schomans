using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class StudentParentConfiguration : IEntityTypeConfiguration<StudentParent>
{
    public void Configure(EntityTypeBuilder<StudentParent> builder)
    {
        builder.HasOne(p => p.ParentUser).WithMany().HasForeignKey(p => p.ParentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ParentUserId, p.StudentId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(p => p.StudentId);
    }
}
