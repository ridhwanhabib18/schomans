using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Ai;

namespace SchoMans.Application.Features.Ai;

/// <summary>
/// Every AI call of a use case goes through here: it refuses the call when the user has used the day's calls or the month's
/// budget is spent (with a clear message), and logs every call that reaches a provider (feature, user, tokens, cost, duration).
/// </summary>
public sealed class AiGateway(IAppDbContext db, IAiService ai, ISchoolClock clock, IOptions<AiOptions> options)
{
    private readonly AiOptions _options = options.Value;

    public string Provider => ai.Provider;

    /// <summary>What a user may still do today, for the screens that offer an AI button.</summary>
    public async Task<AiAllowance> GetAllowanceAsync(Guid userId, CancellationToken ct)
    {
        var used = await CallsTodayAsync(userId, ct);
        var spent = await SpentThisMonthAsync(ct);
        return new AiAllowance(
            ai.Provider,
            Math.Max(0, _options.DailyCallsPerUser - used),
            _options.DailyCallsPerUser,
            spent < _options.MonthlyBudgetUsd);
    }

    /// <param name="userId">The person asking, or null when the system asks (a job); then only the budget applies.</param>
    /// <param name="bulk">
    /// A further call of one bulk run (the first call of the run is an ordinary one). It still needs the user to be within the
    /// daily limit and counts toward the budget, but it does not use up another of the user's daily calls.
    /// </param>
    public async Task<Result<AiCompletion>> RunAsync(Guid? userId, AiRequest request, CancellationToken ct, bool bulk = false)
    {
        if (await RefusalAsync(userId, ct) is { } refusal)
        {
            return refusal;
        }

        var watch = Stopwatch.StartNew();
        var result = await ai.CompleteAsync(request, ct);
        watch.Stop();

        var completion = result.IsSuccess ? result.Value : null;
        await LogAsync(userId, request.Feature, completion?.Provider, completion?.Model, completion?.Tokens ?? 0, completion?.CostUsd ?? 0m, watch, result.IsSuccess, bulk, ct);
        return result;
    }

    /// <summary>One step of a conversation with tools. The same limits and the same log as <see cref="RunAsync"/>; see there for <paramref name="bulk"/>.</summary>
    public async Task<Result<AiChatReply>> ChatAsync(Guid? userId, AiChatRequest request, CancellationToken ct, bool bulk = false)
    {
        if (await RefusalAsync(userId, ct) is { } refusal)
        {
            return refusal;
        }

        var watch = Stopwatch.StartNew();
        var result = await ai.ChatAsync(request, ct);
        watch.Stop();

        var reply = result.IsSuccess ? result.Value : null;
        await LogAsync(userId, request.Feature, reply?.Provider, reply?.Model, reply?.Tokens ?? 0, reply?.CostUsd ?? 0m, watch, result.IsSuccess, bulk, ct);
        return result;
    }

    private async Task<Error?> RefusalAsync(Guid? userId, CancellationToken ct)
    {
        if (await SpentThisMonthAsync(ct) >= _options.MonthlyBudgetUsd)
        {
            return Error.TooManyRequests("ai.budget_exhausted", "The AI budget of this month is used up. The AI features are switched off until next month.");
        }

        if (userId is { } id && await CallsTodayAsync(id, ct) >= _options.DailyCallsPerUser)
        {
            return Error.TooManyRequests("ai.daily_limit", $"You have used your {_options.DailyCallsPerUser} AI calls of today. Try again tomorrow.");
        }

        return null;
    }

    private async Task LogAsync(Guid? userId, string feature, string? provider, string? model, int tokens, decimal costUsd, Stopwatch watch, bool succeeded, bool bulk, CancellationToken ct)
    {
        db.AiUsageLogs.Add(AiUsageLog.Create(userId, feature, provider ?? ai.Provider, model ?? string.Empty, tokens, costUsd, (int)watch.ElapsedMilliseconds, succeeded, bulk));
        await db.SaveChangesAsync(ct);
    }

    private Task<int> CallsTodayAsync(Guid userId, CancellationToken ct)
    {
        var from = clock.ToUtc(clock.Today, TimeOnly.MinValue);
        return db.AiUsageLogs.CountAsync(l => l.UserId == userId && !l.IsBulk && l.CreatedAt >= from, ct);
    }

    private async Task<decimal> SpentThisMonthAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var from = clock.ToUtc(new DateOnly(today.Year, today.Month, 1), TimeOnly.MinValue);
        return await db.AiUsageLogs.Where(l => l.CreatedAt >= from).SumAsync(l => (decimal?)l.CostUsd, ct) ?? 0m;
    }
}

public sealed record AiAllowance(string Provider, int CallsLeftToday, int DailyCalls, bool BudgetAvailable);
