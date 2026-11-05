using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class GeneralLetterConfiguration : IEntityTypeConfiguration<GeneralLetter>
{
    public void Configure(EntityTypeBuilder<GeneralLetter> builder)
    {
        builder.Property(l => l.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(l => l.LetterNumber).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Recipient).HasMaxLength(GeneralLetter.RecipientMaxLength).IsRequired();
        builder.Property(l => l.Subject).HasMaxLength(GeneralLetter.SubjectMaxLength).IsRequired();
        builder.Property(l => l.Body).HasMaxLength(GeneralLetter.BodyMaxLength).IsRequired();
        builder.Property(l => l.SignedByName).HasMaxLength(150).IsRequired();
        builder.Property(l => l.PdfKey).HasMaxLength(300).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(l => l.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);

        // The number is the next one of its kind in the year; it is unique, so two letters issued at once cannot share it.
        builder.HasIndex(l => l.LetterNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(l => l.IssuedAt);
    }
}
