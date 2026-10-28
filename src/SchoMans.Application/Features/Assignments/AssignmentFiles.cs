using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments;

/// <summary>
/// Which files assignments accept and where they live in the private bucket. Teachers attach PDFs, images and Office
/// files; students hand in a PDF or an image. Both at most 10 MB.
/// </summary>
public static class AssignmentFileRules
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> AttachmentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = "pdf",
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "pptx",
    };

    public static readonly IReadOnlyDictionary<string, string> SubmissionTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = "pdf",
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };
}

internal static class AssignmentFiles
{
    public static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);

    /// <summary>A teacher uploads attachments before the assignment exists, so they live under the teacher until saved with one.</summary>
    public static string NewAttachmentKey(Guid teacherId, string contentType) =>
        $"assignments/uploads/{teacherId}/{Guid.NewGuid():N}.{AssignmentFileRules.AttachmentTypes[contentType]}";

    public static string NewSubmissionKey(Guid assignmentId, Guid studentId, string contentType) =>
        $"assignments/{assignmentId}/submissions/{studentId}/{Guid.NewGuid():N}.{AssignmentFileRules.SubmissionTypes[contentType]}";

    public static bool IsAttachmentKeyOf(string key, Guid teacherId) =>
        key.StartsWith($"assignments/uploads/{teacherId}/", StringComparison.Ordinal) && !key.Contains("..", StringComparison.Ordinal);

    public static bool IsSubmissionKeyOf(string key, Guid assignmentId, Guid studentId) =>
        key.StartsWith($"assignments/{assignmentId}/submissions/{studentId}/", StringComparison.Ordinal) && !key.Contains("..", StringComparison.Ordinal);

    /// <summary>The file must exist and be of an accepted type and size. A file that breaks the rules is deleted.</summary>
    public static async Task<Error?> VerifyAsync(IFileStorage storage, string key, IReadOnlyDictionary<string, string> types, CancellationToken ct)
    {
        var info = await storage.GetObjectInfoAsync(FileBucket.Private, key, ct);
        if (info is null)
        {
            return Error.Validation("files.not_uploaded", "The file was not uploaded. Upload it first.");
        }

        if (info.SizeBytes > AssignmentFileRules.MaxBytes || info.ContentType is null || !types.ContainsKey(info.ContentType))
        {
            await storage.DeleteAsync(FileBucket.Private, key, ct);
            return Error.Validation("files.invalid_file", "The file type or size is not accepted.");
        }

        return null;
    }
}
