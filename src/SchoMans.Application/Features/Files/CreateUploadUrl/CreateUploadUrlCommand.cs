namespace SchoMans.Application.Features.Files.CreateUploadUrl;

/// <param name="Module">"news" or "announcements".</param>
/// <param name="EntityId">The news article or announcement the image is for; it must exist (save the draft first).</param>
public sealed record CreateUploadUrlCommand(string Module, Guid EntityId, string ContentType, long SizeBytes);

/// <param name="Key">Send this key back when saving the record. The browser must PUT the file with exactly this Content-Type.</param>
public sealed record UploadUrlDto(string Url, string Key, string ContentType, DateTimeOffset ExpiresAt);
