using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.Property(a => a.Title).HasMaxLength(Assignment.TitleMaxLength).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(Assignment.DescriptionMaxLength).IsRequired();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.Component).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasOne(a => a.Teacher).WithMany().HasForeignKey(a => a.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Subject).WithMany().HasForeignKey(a => a.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Term).WithMany().HasForeignKey(a => a.TermId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Classrooms).WithOne().HasForeignKey(c => c.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Attachments).WithOne().HasForeignKey(f => f.AssignmentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.TeacherId, a.DueAt });
        builder.HasIndex(a => a.DueAt);
    }
}

internal sealed class AssignmentClassroomConfiguration : IEntityTypeConfiguration<AssignmentClassroom>
{
    public void Configure(EntityTypeBuilder<AssignmentClassroom> builder)
    {
        builder.ToTable("AssignmentClassrooms");
        builder.HasKey(c => new { c.AssignmentId, c.ClassroomId });
        builder.HasOne<Classroom>().WithMany().HasForeignKey(c => c.ClassroomId).OnDelete(DeleteBehavior.Restrict);

        // A student's list starts from their classroom.
        builder.HasIndex(c => c.ClassroomId);
    }
}

internal sealed class AssignmentAttachmentConfiguration : IEntityTypeConfiguration<AssignmentAttachment>
{
    public void Configure(EntityTypeBuilder<AssignmentAttachment> builder)
    {
        builder.ToTable("AssignmentAttachments");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.FileKey).HasMaxLength(300).IsRequired();
        builder.Property(f => f.FileName).HasMaxLength(AssignmentAttachment.FileNameMaxLength).IsRequired();
    }
}
