using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Rubrics;

/// <summary>One criterion of a rubric: what is judged, how it is described, and the top of its score range (0 to <paramref name="MaxPoints"/>).</summary>
public sealed record RubricCriterion(string Name, string Description, int MaxPoints);

public sealed record RubricDto(Guid AssignmentId, IReadOnlyList<RubricCriterion> Criteria, int TotalPoints);

internal static class RubricJson
{
    public const int MaxCriteria = 6;
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 300;
    public const int MaxPointsLimit = 100;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(IEnumerable<RubricCriterion> criteria) => JsonSerializer.Serialize(criteria, Options);

    public static List<RubricCriterion> Read(string json) => JsonSerializer.Deserialize<List<RubricCriterion>>(json, Options) ?? [];

    public static RubricDto ToDto(Guid assignmentId, string json)
    {
        var criteria = Read(json);
        return new RubricDto(assignmentId, criteria, criteria.Sum(c => c.MaxPoints));
    }

    /// <summary>The assignment, when it exists and was given by the signed-in teacher.</summary>
    public static async Task<Result<Assignment>> OwnAssignmentAsync(IAppDbContext db, ICurrentUser currentUser, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.Assignments.FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment is null)
        {
            return Error.NotFound("assignments.not_found", "The assignment does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != assignment.TeacherId)
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the assignment can change its rubric.");
        }

        return assignment;
    }
}
