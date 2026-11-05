using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Chatbot;

namespace SchoMans.Api.Endpoints;

public static class ChatbotEndpoints
{
    public static IEndpointRouteBuilder MapChatbotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/chatbot")
            .WithTags("Chatbot")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Anybody: the landing page. Limited per client address to keep abuse and cost down.
        group.MapPost("/public", AskPublic)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Chatbot)
            .WithValidation<AskChatbotCommand>()
            .WithName("AskPublicChatbot");

        // Parents: the dashboard, with the announcements meant for them.
        group.MapPost("/parent", AskParent)
            .RequireAuthorization(Policies.Parent)
            .RequireRateLimiting(RateLimitPolicies.Chatbot)
            .WithValidation<AskChatbotCommand>()
            .WithName("AskParentChatbot")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<Results<Ok<ChatbotAnswerDto>, ProblemHttpResult>> AskPublic(
        [FromBody] AskChatbotCommand command, AskPublicChatbotHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<ChatbotAnswerDto>, ProblemHttpResult>> AskParent(
        [FromBody] AskChatbotCommand command, AskParentChatbotHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
