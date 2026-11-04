namespace SchoMans.Application.Features.Ai;

/// <summary>Configuration section <c>Ai</c>. The default provider is the mock, so nothing needs a key or costs money (ADR-004).</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary><c>Mock</c>, <c>Ollama</c> or <c>Claude</c>.</summary>
    public string Provider { get; set; } = AiProviders.Mock;

    /// <summary>Calls one user may make per school day. Calls of the system itself (jobs) are not counted here.</summary>
    public int DailyCallsPerUser { get; set; } = 10;

    /// <summary>The cost of all calls in a calendar month, in USD. Free providers cost 0, so they never reach it.</summary>
    public decimal MonthlyBudgetUsd { get; set; } = 5m;

    public OllamaOptions Ollama { get; set; } = new();

    public ClaudeOptions Claude { get; set; } = new();
}

public static class AiProviders
{
    public const string Mock = "Mock";
    public const string Ollama = "Ollama";
    public const string Claude = "Claude";
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    public string Model { get; set; } = "qwen2.5:7b";

    /// <summary>A local model can take minutes on a CPU.</summary>
    public int TimeoutSeconds { get; set; } = 180;
}

public sealed class ClaudeOptions
{
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The cheapest current model; change it when quality asks for more.</summary>
    public string Model { get; set; } = "claude-haiku-5-5";

    public int MaxTokens { get; set; } = 2000;

    /// <summary>Prices in USD per million tokens, for the usage log and the budget (Haiku 5.5, prompts up to 100K tokens).</summary>
    public decimal InputUsdPerMillion { get; set; } = 0.10m;

    public decimal OutputUsdPerMillion { get; set; } = 0.50m;

    public int TimeoutSeconds { get; set; } = 60;
}
