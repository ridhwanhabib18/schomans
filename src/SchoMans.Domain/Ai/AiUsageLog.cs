using SchoMans.Domain.Common;

namespace SchoMans.Domain.Ai;

/// <summary>One call to an AI provider: who asked, for which feature, and what it cost. Counts toward the daily and monthly limits.</summary>
public sealed class AiUsageLog : BaseEntity
{
    private AiUsageLog()
    {
    }

    /// <summary>The person who asked; null when the system itself asked (a monthly job), which counts toward the budget only.</summary>
    public Guid? UserId { get; private set; }

    public string Feature { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public int Tokens { get; private set; }

    public decimal CostUsd { get; private set; }

    public int DurationMs { get; private set; }

    public bool Succeeded { get; private set; }

    /// <summary>A further call of a bulk run (one student of a whole classroom): it counts toward the budget, not toward the user's daily calls.</summary>
    public bool IsBulk { get; private set; }

    public static AiUsageLog Create(
        Guid? userId, string feature, string provider, string model, int tokens, decimal costUsd, int durationMs, bool succeeded, bool isBulk = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentOutOfRangeException.ThrowIfNegative(tokens);
        ArgumentOutOfRangeException.ThrowIfNegative(costUsd);

        return new AiUsageLog
        {
            UserId = userId,
            Feature = feature,
            Provider = provider,
            Model = model,
            Tokens = tokens,
            CostUsd = costUsd,
            DurationMs = durationMs,
            Succeeded = succeeded,
            IsBulk = isBulk,
        };
    }
}
