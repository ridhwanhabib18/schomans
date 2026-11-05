using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Assignments;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.Property(r => r.CriteriaJson).HasMaxLength(Rubric.CriteriaJsonMaxLength).IsRequired();

        builder.HasOne(r => r.Assignment).WithMany().HasForeignKey(r => r.AssignmentId).OnDelete(DeleteBehavior.Restrict);

        // One rubric per assignment.
        builder.HasIndex(r => r.AssignmentId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class FeedbackDraftConfiguration : IEntityTypeConfiguration<FeedbackDraft>
{
    public void Configure(EntityTypeBuilder<FeedbackDraft> builder)
    {
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.CriteriaJson).HasMaxLength(FeedbackDraft.CriteriaJsonMaxLength).IsRequired();
        builder.Property(d => d.Suggestion).HasMaxLength(FeedbackDraft.SuggestionMaxLength).IsRequired();

        builder.HasOne(d => d.Submission).WithMany().HasForeignKey(d => d.SubmissionId).OnDelete(DeleteBehavior.Restrict);

        // One draft per piece of work; a new AI draft replaces the old one.
        builder.HasIndex(d => d.SubmissionId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
