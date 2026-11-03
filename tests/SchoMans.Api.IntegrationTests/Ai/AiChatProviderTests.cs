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

/// <summary>Tool use (AI-04) in the real Ollama and Claude clients against a scripted server: what they send and how they read tool calls.</summary>
public class AiChatProviderTests
{
    private sealed class ScriptedServer(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly AiTool Tool = new("attendance_by_classroom", "Attendance per classroom.", """{"type":"object","properties":{"classroom":{"type":"string"}}}""");

    private static readonly AiChatRequest FirstStep = new(
        AiFeatures.PrincipalAssistant, "You help the principal.", [new AiChatTurn("user", "How is attendance in 8B?")], [Tool]);

    private static readonly AiChatRequest SecondStep = new(
        AiFeatures.PrincipalAssistant,
        "You help the principal.",
        [
            new AiChatTurn("user", "How is attendance in 8B?"),
            new AiChatTurn("assistant", "Let me look.", [new AiToolCall("call-1", "attendance_by_classroom", """{"classroom":"8B"}""")]),
            new AiChatTurn("tool", ToolResults: [new AiToolResult("call-1", "attendance_by_classroom", """{"summary":"92% present."}""")]),
        ],
        [Tool]);

    private static IOptions<AiOptions> Config() => Options.Create(new AiOptions
    {
        Ollama = new OllamaOptions { BaseUrl = "http://ollama.test/", Model = "qwen-test" },
        Claude = new ClaudeOptions { BaseUrl = "http://claude.test/", ApiKey = "key-1", Model = "claude-haiku-5-5", InputUsdPerMillion = 0.10m, OutputUsdPerMillion = 0.50m },
    });

    private static OllamaAiService Ollama(ScriptedServer server) => new(new HttpClient(server), Config(), NullLogger<OllamaAiService>.Instance);

    private static ClaudeAiService Claude(ScriptedServer server) => new(new HttpClient(server), Config(), NullLogger<ClaudeAiService>.Instance);

    [Fact]
    public async Task Claude_sends_the_tools_and_reads_a_tool_call()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """
            {"content":[{"type":"text","text":"Let me look."},{"type":"tool_use","id":"toolu_1","name":"attendance_by_classroom","input":{"classroom":"8B"}}],
             "stop_reason":"tool_use","usage":{"input_tokens":100,"output_tokens":20}}
            """);

        var result = await Claude(server).ChatAsync(FirstStep, CancellationToken.None);

        using var body = JsonDocument.Parse(server.Body);
        var tool = body.RootElement.GetProperty("tools")[0];
        Assert.Equal("attendance_by_classroom", tool.GetProperty("name").GetString());
        Assert.Equal("object", tool.GetProperty("input_schema").GetProperty("type").GetString());
        Assert.Equal("user", body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        var call = Assert.Single(result.Value.ToolCalls);
        Assert.Equal(("toolu_1", "attendance_by_classroom"), (call.Id, call.Name));
        Assert.Equal("8B", JsonDocument.Parse(call.Arguments).RootElement.GetProperty("classroom").GetString());
        Assert.Equal("Let me look.", result.Value.Text);
        Assert.Equal(120, result.Value.Tokens);
        Assert.Equal(0.00002m, result.Value.CostUsd); // 100 * 0.10 / 1M + 20 * 0.50 / 1M
    }

    [Fact]
    public async Task Claude_returns_the_tool_results_to_the_model_as_a_user_message()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """{"content":[{"type":"text","text":"92% of the lessons were attended."}],"stop_reason":"end_turn","usage":{"input_tokens":150,"output_tokens":10}}""");

        var result = await Claude(server).ChatAsync(SecondStep, CancellationToken.None);

        using var body = JsonDocument.Parse(server.Body);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("tool_use", messages[1].GetProperty("content")[1].GetProperty("type").GetString());
        Assert.Equal("call-1", messages[1].GetProperty("content")[1].GetProperty("id").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.Equal("tool_result", messages[2].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("call-1", messages[2].GetProperty("content")[0].GetProperty("tool_use_id").GetString());
        Assert.Empty(result.Value.ToolCalls);
        Assert.Equal("92% of the lessons were attended.", result.Value.Text);
    }

    [Fact]
    public async Task Ollama_sends_the_tools_as_functions_and_reads_a_tool_call()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """
            {"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"attendance_by_classroom","arguments":{"classroom":"8B"}}}]},
             "prompt_eval_count":30,"eval_count":5}
            """);

        var result = await Ollama(server).ChatAsync(FirstStep, CancellationToken.None);

        using var body = JsonDocument.Parse(server.Body);
        Assert.Equal("function", body.RootElement.GetProperty("tools")[0].GetProperty("type").GetString());
        Assert.Equal("attendance_by_classroom", body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        var call = Assert.Single(result.Value.ToolCalls);
        Assert.Equal("attendance_by_classroom", call.Name);
        Assert.Equal("8B", JsonDocument.Parse(call.Arguments).RootElement.GetProperty("classroom").GetString());
        Assert.Equal(35, result.Value.Tokens);
        Assert.Equal(0m, result.Value.CostUsd);
    }

    [Fact]
    public async Task Ollama_returns_the_tool_results_as_tool_messages()
    {
        var server = new ScriptedServer(HttpStatusCode.OK, """{"message":{"role":"assistant","content":"92% attended."}}""");

        var result = await Ollama(server).ChatAsync(SecondStep, CancellationToken.None);

        using var body = JsonDocument.Parse(server.Body);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[2].GetProperty("role").GetString());
        Assert.Equal("attendance_by_classroom", messages[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("tool", messages[3].GetProperty("role").GetString());
        Assert.Equal("""{"summary":"92% present."}""", messages[3].GetProperty("content").GetString());
        Assert.Equal("92% attended.", result.Value.Text);
        Assert.Empty(result.Value.ToolCalls);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task A_failing_provider_is_reported_as_unavailable_for_chat_too(HttpStatusCode status, string json)
    {
        var ollama = await Ollama(new ScriptedServer(status, json)).ChatAsync(FirstStep, CancellationToken.None);
        var claude = await Claude(new ScriptedServer(status, json)).ChatAsync(FirstStep, CancellationToken.None);

        foreach (var result in new[] { ollama, claude })
        {
            Assert.Equal(ErrorType.Unavailable, result.Error.Type);
            Assert.Equal("ai.provider_failed", result.Error.Code);
        }
    }

    [Fact]
    public async Task Claude_without_a_key_says_it_is_not_configured_for_chat_too()
    {
        var noKey = new ClaudeAiService(new HttpClient(new ScriptedServer(HttpStatusCode.OK, "{}")), Options.Create(new AiOptions()), NullLogger<ClaudeAiService>.Instance);

        var result = await noKey.ChatAsync(FirstStep, CancellationToken.None);

        Assert.Equal("ai.not_configured", result.Error.Code);
    }
}
