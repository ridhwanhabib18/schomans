using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class TeacherNoteConfiguration : IEntityTypeConfiguration<TeacherNote>
{
    public void Configure(EntityTypeBuilder<TeacherNote> builder)
    {
        builder.Property(n => n.Body).HasMaxLength(TeacherNote.BodyMaxLength).IsRequired();

        builder.HasOne(n => n.Student).WithMany().HasForeignKey(n => n.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.Author).WithMany().HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.Restrict);

        // The notes of one student, newest first, and the notes of one month for a summary.
        builder.HasIndex(n => new { n.StudentId, n.Date });
    }
}
