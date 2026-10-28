using SchoMans.Application.Features.Assignments.CreateAssignment;

namespace SchoMans.Application.Features.Assignments.UpdateAssignment;

/// <param name="KeepAttachmentIds">Attachments to keep; the others are removed.</param>
/// <param name="NewAttachments">Files uploaded since, to add.</param>
public sealed record UpdateAssignmentCommand(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset DueAt,
    IReadOnlyList<Guid> ClassroomIds,
    IReadOnlyList<Guid>? KeepAttachmentIds,
    IReadOnlyList<NewAttachment>? NewAttachments,
    string RowVersion);
