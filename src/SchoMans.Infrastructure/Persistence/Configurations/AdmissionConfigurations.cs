using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AdmissionPeriodConfiguration : IEntityTypeConfiguration<AdmissionPeriod>
{
    public void Configure(EntityTypeBuilder<AdmissionPeriod> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(AdmissionPeriod.NameMaxLength).IsRequired();

        builder.HasOne(p => p.AcademicYear).WithMany().HasForeignKey(p => p.AcademicYearId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AcademicYearId);
        builder.HasIndex(p => new { p.OpensAt, p.ClosesAt });
    }
}

internal sealed class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        builder.Property(a => a.FormData).HasMaxLength(Applicant.FormDataMaxLength).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(Applicant.NotesMaxLength);
        builder.Ignore(a => a.IsEditable);
        builder.Ignore(a => a.HasAllDocuments);
        builder.Ignore(a => a.AllDocumentsVerified);
        builder.Ignore(a => a.HasRejectedDocument);

        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AdmissionPeriod).WithMany().HasForeignKey(a => a.AdmissionPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Documents).WithOne().HasForeignKey(d => d.ApplicantId).OnDelete(DeleteBehavior.Restrict);

        // One application per person and period; one applicant per student.
        builder.HasIndex(a => new { a.UserId, a.AdmissionPeriodId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => a.StudentId).IsUnique().HasFilter("[StudentId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(a => new { a.AdmissionPeriodId, a.Status });
    }
}

internal sealed class ApplicantDocumentConfiguration : IEntityTypeConfiguration<ApplicantDocument>
{
    public void Configure(EntityTypeBuilder<ApplicantDocument> builder)
    {
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.FileKey).HasMaxLength(300).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(ApplicantDocument.FileNameMaxLength).IsRequired();
        builder.Property(d => d.ReviewNote).HasMaxLength(ApplicantDocument.NoteMaxLength);

        // One file per kind of document and applicant; uploading again replaces it.
        builder.HasIndex(d => new { d.ApplicantId, d.Type }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
