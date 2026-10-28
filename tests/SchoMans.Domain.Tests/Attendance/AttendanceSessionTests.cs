using SchoMans.Domain.Attendance;

namespace SchoMans.Domain.Tests.Attendance;

public class AttendanceSessionTests
{
    private static readonly DateTimeOffset Opened = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 28);

    private static AttendanceSession Open() => AttendanceSession.Open(Guid.NewGuid(), Today, Opened);

    [Fact]
    public void A_new_session_is_open_with_a_secret_and_remembers_when_it_opened()
    {
        var session = Open();

        Assert.True(session.IsOpen);
        Assert.Equal(Opened, session.OpenedAt);
        Assert.Equal(Opened, session.QrRotatedAt);
        Assert.Null(session.PreviousQrSecret);
        Assert.Equal(22, session.QrSecret.Length); // 16 random bytes, base64url without padding
        Assert.Equal(Today, session.Date);
    }

    [Fact]
    public void Every_session_gets_its_own_secret()
    {
        Assert.NotEqual(Open().QrSecret, Open().QrSecret);
    }

    [Fact]
    public void The_token_carries_the_session_and_its_secret_and_parses_back()
    {
        var session = Open();

        Assert.True(AttendanceSession.TryParseToken(session.QrToken, out var id, out var secret));
        Assert.Equal(session.Id, id);
        Assert.Equal(session.QrSecret, secret);
        Assert.DoesNotContain('+', session.QrToken);
        Assert.DoesNotContain('/', session.QrToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("not-a-guid.secret")]
    [InlineData("0123456789abcdef0123456789abcdef.")]
    [InlineData("0123456789abcdef0123456789abcdef.a.b")]
    [InlineData("https://example.com/attend?t=x")]
    public void Anything_else_is_not_a_token(string? token)
    {
        Assert.False(AttendanceSession.TryParseToken(token, out _, out _));
    }

    [Fact]
    public void The_current_secret_is_accepted()
    {
        var session = Open();

        Assert.True(session.AcceptsSecret(session.QrSecret, Opened.AddSeconds(59)));
        Assert.False(session.AcceptsSecret("wrong", Opened));
    }

    [Fact]
    public void After_a_rotation_the_old_secret_is_accepted_for_ten_seconds_only()
    {
        var session = Open();
        var old = session.QrSecret;
        var rotatedAt = Opened.AddMinutes(1);

        Assert.True(session.RotateQr(rotatedAt));

        Assert.NotEqual(old, session.QrSecret);
        Assert.Equal(old, session.PreviousQrSecret);
        Assert.True(session.AcceptsSecret(old, rotatedAt.AddSeconds(10)));
        Assert.False(session.AcceptsSecret(old, rotatedAt.AddSeconds(11)));
        Assert.True(session.AcceptsSecret(session.QrSecret, rotatedAt.AddSeconds(11)));
    }

    [Fact]
    public void A_secret_two_rotations_old_is_never_accepted()
    {
        var session = Open();
        var first = session.QrSecret;
        session.RotateQr(Opened.AddMinutes(1));
        session.RotateQr(Opened.AddMinutes(1).AddSeconds(1));

        Assert.False(session.AcceptsSecret(first, Opened.AddMinutes(1).AddSeconds(2)));
    }

    [Fact]
    public void A_closed_session_accepts_no_secret_and_no_longer_rotates()
    {
        var session = Open();
        var secret = session.QrSecret;

        Assert.True(session.Close(Opened.AddMinutes(40)));

        Assert.False(session.IsOpen);
        Assert.False(session.AcceptsSecret(secret, Opened.AddMinutes(40)));
        Assert.False(session.RotateQr(Opened.AddMinutes(41)));
        Assert.Equal(secret, session.QrSecret);
    }

    [Fact]
    public void Closing_twice_keeps_the_first_closing_time()
    {
        var session = Open();
        session.Close(Opened.AddMinutes(40));

        Assert.False(session.Close(Opened.AddMinutes(50)));
        Assert.Equal(Opened.AddMinutes(40), session.ClosedAt);
    }
}
