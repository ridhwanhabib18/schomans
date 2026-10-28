using System.Security.Cryptography;
using System.Text;
using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Attendance;

/// <summary>
/// One lesson's attendance: opened by the teacher, scanned by the students through a QR code whose secret rotates every
/// minute, and closed by the teacher (or the system). The previous secret stays valid for a short grace period after a
/// rotation, so a student who scans at the changeover is not turned away.
/// </summary>
public sealed class AttendanceSession : BaseEntity
{
    /// <summary>How long the previous QR secret is still accepted after a rotation.</summary>
    public static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(10);

    private AttendanceSession()
    {
    }

    public Guid ScheduleId { get; private set; }

    public Schedule? Schedule { get; private set; }

    /// <summary>The school day of the lesson (local date).</summary>
    public DateOnly Date { get; private set; }

    public string QrSecret { get; private set; } = string.Empty;

    /// <summary>The secret before the last rotation; accepted until <see cref="RotationGrace"/> after <see cref="QrRotatedAt"/>.</summary>
    public string? PreviousQrSecret { get; private set; }

    public DateTimeOffset QrRotatedAt { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsOpen => ClosedAt is null;

    /// <summary>The value encoded in the QR code: the session and its current secret.</summary>
    public string QrToken => FormatToken(Id, QrSecret);

    public static AttendanceSession Open(Guid scheduleId, DateOnly date, DateTimeOffset now) => new()
    {
        ScheduleId = scheduleId,
        Date = date,
        QrSecret = NewSecret(),
        QrRotatedAt = now,
        OpenedAt = now,
    };

    public static string FormatToken(Guid sessionId, string secret) => $"{sessionId:N}.{secret}";

    /// <summary>Splits a scanned token into the session id and the secret. Returns false for anything that is not a token.</summary>
    public static bool TryParseToken(string? token, out Guid sessionId, out string secret)
    {
        sessionId = Guid.Empty;
        secret = string.Empty;
        var parts = token?.Trim().Split('.');
        if (parts is not { Length: 2 } || !Guid.TryParseExact(parts[0], "N", out sessionId) || parts[1].Length == 0)
        {
            return false;
        }

        secret = parts[1];
        return true;
    }

    /// <summary>Issues a new secret; the old one stays valid for the grace period. Does nothing on a closed session.</summary>
    public bool RotateQr(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return false;
        }

        PreviousQrSecret = QrSecret;
        QrSecret = NewSecret();
        QrRotatedAt = now;
        return true;
    }

    /// <summary>True for the current secret, or for the previous one within the grace period after the rotation.</summary>
    public bool AcceptsSecret(string secret, DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (FixedTimeEquals(secret, QrSecret))
        {
            return true;
        }

        return PreviousQrSecret is not null && now - QrRotatedAt <= RotationGrace && FixedTimeEquals(secret, PreviousQrSecret);
    }

    /// <summary>Closes the session. Returns false when it already was closed.</summary>
    public bool Close(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return false;
        }

        ClosedAt = now;
        return true;
    }

    private static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // The comparison takes the same time whatever the input, so a scanner cannot learn the secret byte by byte.
    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
