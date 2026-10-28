using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.Property(s => s.FileKey).HasMaxLength(300);
        builder.Property(s => s.FileName).HasMaxLength(AssignmentAttachment.FileNameMaxLength);
        builder.Property(s => s.TextBody).HasMaxLength(Submission.TextMaxLength);
        builder.Property(s => s.Feedback).HasMaxLength(Submission.FeedbackMaxLength);
        builder.Property(s => s.Score).HasPrecision(5, 2);
        builder.Ignore(s => s.IsGraded);

        builder.HasOne(s => s.Assignment).WithMany().HasForeignKey(s => s.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.GradedById).OnDelete(DeleteBehavior.Restrict);

        // One submission per student per assignment.
        builder.HasIndex(s => new { s.AssignmentId, s.StudentId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.StudentId);
    }
}
