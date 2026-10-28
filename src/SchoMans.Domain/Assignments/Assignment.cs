using SchoMans.Domain.Common;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Assignments;

/// <summary>Work a teacher gives to one or more classrooms for a subject, with a due date and optional attachments.</summary>
public sealed class Assignment : BaseEntity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 10_000;
    public const int MaxAttachments = 5;

    private Assignment()
    {
    }

    public Guid TeacherId { get; private set; }

    public Teacher? Teacher { get; private set; }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public Guid TermId { get; private set; }

    public Term? Term { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DateTimeOffset DueAt { get; private set; }

    public AssignmentType Type { get; private set; } = AssignmentType.Regular;

    /// <summary>Where the scores of this assignment count in the subject grade (Phase 4).</summary>
    public GradeComponent Component { get; private set; } = GradeComponent.Assignment;

    public List<AssignmentClassroom> Classrooms { get; private set; } = [];

    public List<AssignmentAttachment> Attachments { get; private set; } = [];

    public static Assignment Create(Guid teacherId, Guid subjectId, Guid termId, string title, string description, DateTimeOffset dueAt, IEnumerable<Guid> classroomIds)
    {
        var assignment = new Assignment { TeacherId = teacherId, SubjectId = subjectId, TermId = termId };
        assignment.UpdateDetails(title, description, dueAt);
        assignment.SetClassrooms(classroomIds);
        return assignment;
    }

    public void UpdateDetails(string title, string description, DateTimeOffset dueAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Description = description.Trim();
        DueAt = dueAt;
    }

    /// <summary>Replaces the classrooms the assignment is given to. At least one is required.</summary>
    public void SetClassrooms(IEnumerable<Guid> classroomIds)
    {
        var wanted = classroomIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            throw new ArgumentException("An assignment is given to at least one classroom.", nameof(classroomIds));
        }

        Classrooms.RemoveAll(c => !wanted.Contains(c.ClassroomId));
        foreach (var id in wanted.Where(id => Classrooms.All(c => c.ClassroomId != id)))
        {
            Classrooms.Add(AssignmentClassroom.Create(Id, id));
        }
    }

    /// <summary>Adds an uploaded file. Returns false when the assignment already has the maximum number of attachments.</summary>
    public bool AddAttachment(string fileKey, string fileName)
    {
        if (Attachments.Count >= MaxAttachments)
        {
            return false;
        }

        Attachments.Add(AssignmentAttachment.Create(Id, fileKey, fileName));
        return true;
    }

    public AssignmentAttachment? RemoveAttachment(Guid attachmentId)
    {
        var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is not null)
        {
            Attachments.Remove(attachment);
        }

        return attachment;
    }

    public bool IsLateAt(DateTimeOffset moment) => moment > DueAt;
}
