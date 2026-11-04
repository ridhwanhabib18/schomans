using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Assignments;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(q => q.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(q => q.Difficulty).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(q => q.Body).HasMaxLength(Question.BodyMaxLength).IsRequired();
        builder.Property(q => q.OptionsJson).HasMaxLength(Question.OptionsJsonMaxLength).IsRequired();
        builder.Property(q => q.AnswerKey).HasMaxLength(Question.AnswerKeyMaxLength).IsRequired();
        builder.Property(q => q.Explanation).HasMaxLength(Question.ExplanationMaxLength).IsRequired();
        builder.Ignore(q => q.Options);

        builder.HasOne(q => q.Subject).WithMany().HasForeignKey(q => q.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(q => q.CreatedByTeacher).WithMany().HasForeignKey(q => q.CreatedByTeacherId).OnDelete(DeleteBehavior.Restrict);

        // The bank is read per subject.
        builder.HasIndex(q => new { q.SubjectId, q.Type, q.Difficulty });
    }
}
