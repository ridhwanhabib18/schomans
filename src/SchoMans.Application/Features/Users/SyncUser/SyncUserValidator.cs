using FluentValidation;

namespace SchoMans.Application.Features.Users.SyncUser;

public sealed class SyncUserValidator : AbstractValidator<SyncUserCommand>
{
    public SyncUserValidator()
    {
        RuleFor(x => x.Sub).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RealmRoles).NotNull();
    }
}
