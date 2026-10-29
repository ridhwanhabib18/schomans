using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class ApprovalChainStepConfiguration : IEntityTypeConfiguration<ApprovalChainStep>
{
    public void Configure(EntityTypeBuilder<ApprovalChainStep> builder)
    {
        builder.Property(s => s.RequestType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.ApproverRole).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.RequestType, s.StepOrder }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Reason).HasMaxLength(Request.ReasonMaxLength).IsRequired();
        builder.Ignore(r => r.IsPending);

        builder.HasOne(r => r.Requester).WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Approvals).WithOne().HasForeignKey(a => a.RequestId).OnDelete(DeleteBehavior.Restrict);

        // The inbox of an approver (pending per step) and the list of a requester.
        builder.HasIndex(r => new { r.Status, r.Type, r.CurrentStep });
        builder.HasIndex(r => r.RequesterId);
        builder.HasIndex(r => r.StudentId);
    }
}

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.Property(a => a.Decision).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(Request.NoteMaxLength);

        builder.HasOne(a => a.Approver).WithMany().HasForeignKey(a => a.ApproverId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.RequestId, a.Step });
    }
}

internal sealed class LetterConfiguration : IEntityTypeConfiguration<Letter>
{
    public void Configure(EntityTypeBuilder<Letter> builder)
    {
        builder.Property(l => l.LetterNumber).HasMaxLength(40).IsRequired();
        builder.Property(l => l.VerificationCode).HasMaxLength(40).IsRequired();
        builder.Property(l => l.PdfKey).HasMaxLength(300).IsRequired();

        builder.HasOne(l => l.Request).WithMany().HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Restrict);

        // The authenticity check looks a letter up by its code; the number is unique and so is the letter of a request.
        builder.HasIndex(l => l.LetterNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(l => l.VerificationCode).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(l => l.RequestId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
