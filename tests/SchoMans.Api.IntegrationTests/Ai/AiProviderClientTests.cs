using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Infrastructure.Ai;

namespace SchoMans.Api.IntegrationTests.Ai;

/// <summary>The real Ollama and Claude clients against a scripted server: what they send, how they read the answer, and how they fail.</summary>
public class AiProviderClientTests
{
    private sealed class ScriptedServer(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly AiRequest Ask = new(AiFeatures.ProgressSummary, "You write summaries.", "Summarise S1.", JsonOutput: true);

    private static IOptions<AiOptions> Config(string apiKey = "key-1") => Options.Create(new AiOptions
    {
        Ollama = new OllamaOptions { BaseUrl = "http://ollama.test/", Model = "qwen-test" },
        Claude = new ClaudeOptions { BaseUrl = "http://claude.test/", ApiKey = apiKey, Model = "claude-haiku-5-5", InputUsdPerMillion = 0.10m, OutputUsdPerMillion = 0.50m },
    });

    private static OllamaAiService Ollama(ScriptedServer server) => new(new HttpClient(server), Config(), NullLogger<OllamaAiService>.Instance);

    private static ClaudeAiService Claude(ScriptedServer server, string apiKey = "key-1") => new(new HttpClient(server), Config(apiKey), NullLogger<ClaudeAiService>.Instance);

    // ---- Ollama ----

    [Fact]
    public async Task Ollama_gets_the_chat_request_in_json_mode_and_costs_nothing()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"{\"ok\":1}"},"prompt_eval_count":40,"eval_count":10}""");

        var result = await Ollama(server).CompleteAsync(Ask, CancellationToken.None);

        Assert.Equal("/api/chat", server.Request!.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(server.Body);
        Assert.Equal("qwen-test", body.RootElement.GetProperty("model").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("json", body.RootElement.GetProperty("format").GetString());
        Assert.Equal("system", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("Summarise S1.", body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.Equal("{\"ok\":1}", result.Value.Text);
        Assert.Equal(50, result.Value.Tokens);
        Assert.Equal(0m, result.Value.CostUsd);
        Assert.Equal("Ollama", result.Value.Provider);
    }

    [Fact]
    public async Task Ollama_leaves_json_mode_off_for_plain_text()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """{"message":{"content":"Hello"}}""");

        await Ollama(server).CompleteAsync(Ask with { JsonOutput = false }, CancellationToken.None);

        Assert.False(JsonDocument.Parse(server.Body).RootElement.TryGetProperty("format", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task Ollama_reports_a_failure_as_unavailable(HttpStatusCode status, string json)
    {
        var result = await Ollama(new ScriptedServer(status, json)).CompleteAsync(Ask, CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
        Assert.Equal("ai.provider_failed", result.Error.Code);
    }

    // ---- Claude ----

    [Fact]
    public async Task Claude_sends_the_key_the_cached_system_prompt_and_low_effort_and_prices_the_usage()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """
            {"content":[{"type":"thinking","thinking":""},{"type":"text","text":"{\"a\":1}"}],"stop_reason":"end_turn",
             "usage":{"input_tokens":1000,"cache_creation_input_tokens":500,"cache_read_input_tokens":500,"output_tokens":2000}}
            """);

        var result = await Claude(server).CompleteAsync(Ask, CancellationToken.None);

        Assert.Equal("/v1/messages", server.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("key-1", server.Request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", server.Request.Headers.GetValues("anthropic-version").Single());
        using var body = JsonDocument.Parse(server.Body);
        Assert.Equal("claude-haiku-5-5", body.RootElement.GetProperty("model").GetString());
        var system = body.RootElement.GetProperty("system")[0];
        Assert.Equal("ephemeral", system.GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Contains("one JSON object", system.GetProperty("text").GetString());
        Assert.Equal("low", body.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.False(body.RootElement.TryGetProperty("temperature", out _));

        Assert.Equal("{\"a\":1}", result.Value.Text);
        Assert.Equal(4000, result.Value.Tokens);

        // 2000 input tokens at $0.10 and 2000 output tokens at $0.50 per million.
        Assert.Equal(0.0012m, result.Value.CostUsd);
    }

    [Fact]
    public async Task Claude_asks_for_plain_text_without_the_json_instruction()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """{"content":[{"type":"text","text":"Hi"}],"usage":{"input_tokens":1,"output_tokens":1}}""");

        await Claude(server).CompleteAsync(Ask with { JsonOutput = false }, CancellationToken.None);

        Assert.DoesNotContain("JSON", JsonDocument.Parse(server.Body).RootElement.GetProperty("system")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Claude_without_a_key_does_not_call_out()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, "{}");

        var result = await Claude(server, apiKey: "").CompleteAsync(Ask, CancellationToken.None);

        Assert.Equal("ai.not_configured", result.Error.Code);
        Assert.Null(server.Request);
    }

    [Fact]
    public async Task Claude_reports_a_refusal_as_an_error_instead_of_text()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """{"content":[],"stop_reason":"refusal","usage":{"input_tokens":1,"output_tokens":0}}""");

        var result = await Claude(server).CompleteAsync(Ask, CancellationToken.None);

        Assert.Equal("ai.refused", result.Error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>")]
    [InlineData(HttpStatusCode.OK, "{\"stop_reason\":\"end_turn\"}")]
    public async Task Claude_reports_a_failure_as_unavailable(HttpStatusCode status, string json)
    {
        var result = await Claude(new ScriptedServer(status, json)).CompleteAsync(Ask, CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
        Assert.Equal("ai.provider_failed", result.Error.Code);
    }

    // ---- the mock ----

    [Fact]
    public async Task The_mock_answers_without_a_network_and_its_tokens_follow_the_prompt()
    {
        IAiService mock = new MockAiService();

        var short_ = await mock.CompleteAsync(Ask, CancellationToken.None);
        var long_ = await mock.CompleteAsync(Ask with { UserPrompt = new string('x', 4000) }, CancellationToken.None);

        Assert.True(long_.Value.Tokens > short_.Value.Tokens);
        Assert.Equal("This is an answer of the mock AI.", (await mock.CompleteAsync(Ask with { JsonOutput = false }, CancellationToken.None)).Value.Text);
    }
}
