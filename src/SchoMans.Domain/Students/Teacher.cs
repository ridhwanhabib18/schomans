using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

public sealed class Teacher : BaseEntity
{
    private Teacher()
    {
    }

    public Guid UserId { get; private set; }

    public User? User { get; private set; }

    public string Nip { get; private set; } = string.Empty;

    public static Teacher Create(Guid userId, string nip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nip);

        return new Teacher { UserId = userId, Nip = nip.Trim() };
    }
}
