using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Rubrics;

public sealed record SetRubricCommand(Guid AssignmentId, List<RubricCriterion> Criteria);

public sealed class SetRubricValidator : AbstractValidator<SetRubricCommand>
{
    public SetRubricValidator()
    {
        RuleFor(x => x.Criteria).NotNull().Must(c => c is { Count: >= 1 and <= RubricJson.MaxCriteria })
            .WithMessage($"A rubric has 1 to {RubricJson.MaxCriteria} criteria.");
        RuleFor(x => x.Criteria).Must(c => c.Select(x => x.Name?.Trim().ToLowerInvariant()).Distinct().Count() == c.Count)
            .When(x => x.Criteria is not null)
            .WithMessage("Every criterion has its own name.");
        RuleForEach(x => x.Criteria).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).NotEmpty().MaximumLength(RubricJson.NameMaxLength);
            c.RuleFor(x => x.Description).NotNull().MaximumLength(RubricJson.DescriptionMaxLength);
            c.RuleFor(x => x.MaxPoints).InclusiveBetween(1, RubricJson.MaxPointsLimit);
        });
    }
}

/// <summary>The teacher of an assignment sets (or replaces) its rubric. Drafts already written keep the criteria they were written with.</summary>
public sealed class SetRubricHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<RubricDto>> Handle(SetRubricCommand command, CancellationToken ct)
    {
        var assignment = await RubricJson.OwnAssignmentAsync(db, currentUser, command.AssignmentId, ct);
        if (!assignment.IsSuccess)
        {
            return assignment.Error;
        }

        var criteria = command.Criteria.Select(c => new RubricCriterion(c.Name.Trim(), c.Description.Trim(), c.MaxPoints)).ToList();
        var json = RubricJson.Write(criteria);
        if (json.Length > Rubric.CriteriaJsonMaxLength)
        {
            return Error.Validation("rubrics.too_long", "The rubric is too long; shorten the descriptions.");
        }

        var rubric = await db.Rubrics.FirstOrDefaultAsync(r => r.AssignmentId == command.AssignmentId, ct);
        if (rubric is null)
        {
            db.Rubrics.Add(Rubric.Create(command.AssignmentId, json));
        }
        else
        {
            rubric.Replace(json);
        }

        await db.SaveChangesAsync(ct);
        return RubricJson.ToDto(command.AssignmentId, json);
    }
}

public sealed record GetRubricQuery(Guid AssignmentId);

/// <summary>The rubric of an assignment for the teacher who gave it; without a rubric the list of criteria is empty.</summary>
public sealed class GetRubricHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<RubricDto>> Handle(GetRubricQuery query, CancellationToken ct)
    {
        var assignment = await RubricJson.OwnAssignmentAsync(db, currentUser, query.AssignmentId, ct);
        if (!assignment.IsSuccess)
        {
            return assignment.Error;
        }

        var json = await db.Rubrics.AsNoTracking().Where(r => r.AssignmentId == query.AssignmentId).Select(r => r.CriteriaJson).FirstOrDefaultAsync(ct);
        return json is null ? new RubricDto(query.AssignmentId, [], 0) : RubricJson.ToDto(query.AssignmentId, json);
    }
}
