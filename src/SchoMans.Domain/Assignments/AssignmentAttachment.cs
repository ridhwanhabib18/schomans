namespace SchoMans.Domain.Assignments;

/// <summary>A file the teacher attached to an assignment. The file lives in the private bucket under <see cref="FileKey"/>.</summary>
public sealed class AssignmentAttachment
{
    public const int FileNameMaxLength = 200;

    private AssignmentAttachment()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid AssignmentId { get; private set; }

    public string FileKey { get; private set; } = string.Empty;

    /// <summary>The name the teacher's file had, shown to students and used for the download.</summary>
    public string FileName { get; private set; } = string.Empty;

    public static AssignmentAttachment Create(Guid assignmentId, string fileKey, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var name = fileName.Trim();
        return new AssignmentAttachment
        {
            AssignmentId = assignmentId,
            FileKey = fileKey,
            FileName = name.Length > FileNameMaxLength ? name[..FileNameMaxLength] : name,
        };
    }
}
