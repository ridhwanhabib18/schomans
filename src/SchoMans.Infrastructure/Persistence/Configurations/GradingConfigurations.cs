using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class GradeWeightConfiguration : IEntityTypeConfiguration<GradeWeight>
{
    public void Configure(EntityTypeBuilder<GradeWeight> builder)
    {
        builder.Property(w => w.Component).HasConversion<string>().HasMaxLength(20);
        builder.Property(w => w.WeightPercent).HasPrecision(5, 2);

        builder.HasOne(w => w.Subject).WithMany().HasForeignKey(w => w.SubjectId).OnDelete(DeleteBehavior.Restrict);

        // One weight per component per subject.
        builder.HasIndex(w => new { w.SubjectId, w.Component }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.Property(g => g.Component).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Score).HasPrecision(5, 2);

        builder.HasOne(g => g.Student).WithMany().HasForeignKey(g => g.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Subject).WithMany().HasForeignKey(g => g.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Term).WithMany().HasForeignKey(g => g.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Submission).WithMany().HasForeignKey(g => g.SubmissionId).OnDelete(DeleteBehavior.Restrict);

        // Report-card and dashboard queries.
        builder.HasIndex(g => new { g.StudentId, g.TermId });

        // A scored submission has exactly one grade.
        builder.HasIndex(g => g.SubmissionId).IsUnique().HasFilter("[SubmissionId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

internal sealed class GradePublicationConfiguration : IEntityTypeConfiguration<GradePublication>
{
    public void Configure(EntityTypeBuilder<GradePublication> builder)
    {
        builder.HasOne(p => p.Classroom).WithMany().HasForeignKey(p => p.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Term).WithMany().HasForeignKey(p => p.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.PublishedById).OnDelete(DeleteBehavior.Restrict);

        // Grades are published once per classroom and term.
        builder.HasIndex(p => new { p.ClassroomId, p.TermId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class ReportCardConfiguration : IEntityTypeConfiguration<ReportCard>
{
    public void Configure(EntityTypeBuilder<ReportCard> builder)
    {
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.PdfKey).HasMaxLength(300);

        builder.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Term).WithMany().HasForeignKey(r => r.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Descriptions).WithOne().HasForeignKey(d => d.ReportCardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.StudentId, r.TermId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class AchievementDescriptionConfiguration : IEntityTypeConfiguration<AchievementDescription>
{
    public void Configure(EntityTypeBuilder<AchievementDescription> builder)
    {
        builder.Property(d => d.Body).HasMaxLength(AchievementDescription.BodyMaxLength).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(d => d.Subject).WithMany().HasForeignKey(d => d.SubjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.ReportCardId, d.SubjectId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class GradeBandConfiguration : IEntityTypeConfiguration<GradeBand>
{
    public void Configure(EntityTypeBuilder<GradeBand> builder)
    {
        builder.Property(b => b.Label).HasMaxLength(GradeBand.LabelMaxLength).IsRequired();
        builder.Property(b => b.MinScore).HasPrecision(5, 2);

        builder.HasIndex(b => b.Label).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(b => b.MinScore).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
