using FluentValidation;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Features.Users.SyncUser;
using SchoMans.Infrastructure.Identity;

namespace SchoMans.Api.Middleware;

/// <summary>
/// Runs after authorization. For a signed-in request it syncs the Keycloak identity to the User table (by <c>sub</c>)
/// and fills <see cref="CurrentUser"/>, so handlers can use <c>ICurrentUser</c> for their ownership checks.
/// </summary>
public sealed class UserSyncMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        SyncUserHandler handler,
        IValidator<SyncUserCommand> validator,
        CurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var command = new SyncUserCommand(
            context.User.FindFirst("sub")?.Value ?? string.Empty,
            KeycloakClaims.GetEmail(context.User) ?? string.Empty,
            KeycloakClaims.GetFullName(context.User) ?? string.Empty,
            KeycloakClaims.GetRealmRoles(context.User));

        var validation = await validator.ValidateAsync(command, context.RequestAborted);
        if (!validation.IsValid)
        {
            // The token is valid but lacks the claims SchoMans needs (sub, email, name).
            var error = Error.Unauthorized("auth.incomplete_token", "The token is missing required claims.");
            await error.ToProblem().ExecuteAsync(context);
            return;
        }

        var result = await handler.Handle(command, context.RequestAborted);
        if (result.IsFailure)
        {
            await result.Error.ToProblem().ExecuteAsync(context);
            return;
        }

        currentUser.Set(result.Value);
        await next(context);
    }
}
