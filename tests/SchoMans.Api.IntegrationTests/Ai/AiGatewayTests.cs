using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Infrastructure.Ai;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Ai;

/// <summary>
/// "Daily per-user limits and a monthly budget cap; every call is logged": the gateway on a real database, with a provider
/// that answers what the test says. The class has a database of its own, so the month's sum is only what the test spent.
/// </summary>
public class AiGatewayTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class ScriptedAi(decimal cost = 0m, bool fail = false) : IAiService
    {
        public int Calls { get; private set; }

        public string Provider => "Scripted";

        public Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<Result<AiCompletion>>(fail
                ? Error.Unavailable("ai.provider_failed", "down")
                : new AiCompletion("{}", Provider, "scripted-1", 100, cost));
        }
    }

    private static readonly AiRequest Ask = new(AiFeatures.ProgressSummary, "system", "user", JsonOutput: true);

    private async Task<(AiGateway Gateway, AppDbContext Db, ScriptedAi Ai, Guid UserId, IServiceScope Scope)> ArrangeAsync(
        int dailyCalls = 10,
        decimal budget = 5m,
        decimal cost = 0m,
        bool fail = false)
    {
        var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ai = new ScriptedAi(cost, fail);
        var gateway = new AiGateway(db, ai, scope.ServiceProvider.GetRequiredService<ISchoolClock>(), Options.Create(new AiOptions { DailyCallsPerUser = dailyCalls, MonthlyBudgetUsd = budget }));
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        await db.AiUsageLogs.ExecuteDeleteAsync();
        return (gateway, db, ai, userId, scope);
    }

    [Fact]
    public async Task A_call_is_logged_with_its_feature_user_tokens_cost_and_duration()
    {
        var (gateway, db, _, userId, scope) = await ArrangeAsync(cost: 0.25m);
        using var _scope = scope;

        var result = await gateway.RunAsync(userId, Ask, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var log = await db.AiUsageLogs.AsNoTracking().SingleAsync();
        Assert.Equal(userId, log.UserId);
        Assert.Equal(AiFeatures.ProgressSummary, log.Feature);
        Assert.Equal("Scripted", log.Provider);
        Assert.Equal("scripted-1", log.Model);
        Assert.Equal(100, log.Tokens);
        Assert.Equal(0.25m, log.CostUsd);
        Assert.True(log.DurationMs >= 0);
        Assert.True(log.Succeeded);
    }

    [Fact]
    public async Task A_failed_call_is_logged_too_and_counts_toward_the_day()
    {
        var (gateway, db, _, userId, scope) = await ArrangeAsync(dailyCalls: 1, fail: true);
        using var _scope = scope;

        var first = await gateway.RunAsync(userId, Ask, CancellationToken.None);
        var second = await gateway.RunAsync(userId, Ask, CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, first.Error.Type);
        Assert.Equal("ai.daily_limit", second.Error.Code);
        Assert.False((await db.AiUsageLogs.AsNoTracking().SingleAsync()).Succeeded);
    }

    [Fact]
    public async Task The_daily_limit_stops_a_user_with_a_clear_message_and_does_not_call_the_provider()
    {
        var (gateway, _, ai, userId, scope) = await ArrangeAsync(dailyCalls: 2);
        using var _scope = scope;

        Assert.True((await gateway.RunAsync(userId, Ask, CancellationToken.None)).IsSuccess);
        Assert.True((await gateway.RunAsync(userId, Ask, CancellationToken.None)).IsSuccess);
        var third = await gateway.RunAsync(userId, Ask, CancellationToken.None);

        Assert.Equal(ErrorType.TooManyRequests, third.Error.Type);
        Assert.Equal("ai.daily_limit", third.Error.Code);
        Assert.Contains("2 AI calls", third.Error.Message);
        Assert.Equal(2, ai.Calls);
    }

    [Fact]
    public async Task The_daily_limit_is_per_user()
    {
        var (gateway, db, _, userId, scope) = await ArrangeAsync(dailyCalls: 1);
        using var _scope = scope;
        var other = await db.Users.Where(u => u.Id != userId).Select(u => u.Id).FirstAsync();

        Assert.True((await gateway.RunAsync(userId, Ask, CancellationToken.None)).IsSuccess);

        Assert.True((await gateway.RunAsync(other, Ask, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task The_system_has_no_daily_limit_but_its_calls_count_toward_the_budget()
    {
        var (gateway, db, _, _, scope) = await ArrangeAsync(dailyCalls: 1, budget: 0.5m, cost: 0.3m);
        using var _scope = scope;

        Assert.True((await gateway.RunAsync(null, Ask, CancellationToken.None)).IsSuccess);
        Assert.True((await gateway.RunAsync(null, Ask, CancellationToken.None)).IsSuccess);
        var third = await gateway.RunAsync(null, Ask, CancellationToken.None);

        Assert.Equal("ai.budget_exhausted", third.Error.Code);
        Assert.Equal(2, await db.AiUsageLogs.CountAsync());
        Assert.All(await db.AiUsageLogs.AsNoTracking().ToListAsync(), l => Assert.Null(l.UserId));
    }

    [Fact]
    public async Task The_monthly_budget_switches_the_feature_off_for_everybody()
    {
        var (gateway, db, ai, userId, scope) = await ArrangeAsync(budget: 0.5m, cost: 0.5m);
        using var _scope = scope;
        var other = await db.Users.Where(u => u.Id != userId).Select(u => u.Id).FirstAsync();

        Assert.True((await gateway.RunAsync(userId, Ask, CancellationToken.None)).IsSuccess);
        var refused = await gateway.RunAsync(other, Ask, CancellationToken.None);

        Assert.Equal(ErrorType.TooManyRequests, refused.Error.Type);
        Assert.Contains("budget", refused.Error.Message);
        Assert.Equal(1, ai.Calls);
        Assert.False((await gateway.GetAllowanceAsync(other, CancellationToken.None)).BudgetAvailable);
    }

    [Fact]
    public async Task The_allowance_tells_what_is_left_today()
    {
        var (gateway, _, _, userId, scope) = await ArrangeAsync(dailyCalls: 3);
        using var _scope = scope;
        await gateway.RunAsync(userId, Ask, CancellationToken.None);

        var allowance = await gateway.GetAllowanceAsync(userId, CancellationToken.None);

        Assert.Equal(new AiAllowance("Scripted", 2, 3, true), allowance);
    }

    [Fact]
    public async Task The_mock_is_the_default_provider_of_the_host_and_costs_nothing()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        var ai = scope.ServiceProvider.GetRequiredService<IAiService>();
        var result = await ai.CompleteAsync(Ask with { Feature = "Test" }, CancellationToken.None);

        Assert.IsType<MockAiService>(ai);
        Assert.Equal(0m, result.Value.CostUsd);
        Assert.Equal("{\"mock\":true}", result.Value.Text);
    }
}
