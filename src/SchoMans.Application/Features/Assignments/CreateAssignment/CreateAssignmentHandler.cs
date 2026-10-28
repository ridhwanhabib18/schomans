using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments.ListTeachingOptions;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments.CreateAssignment;

/// <summary>
/// A teacher gives an assignment for a subject to classrooms they teach that subject in (from the timetable), in the
/// current term. Attachments uploaded beforehand are checked and saved with it, then the students are notified.
/// </summary>
public sealed class CreateAssignmentHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    INotificationService notifications,
    ISchoolClock clock)
{
    public async Task<Result<AssignmentDto>> Handle(CreateAssignmentCommand command, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("assignments.not_a_teacher", "Only teachers give assignments.");
        }

        var classroomIds = command.ClassroomIds.Distinct().ToList();
        var problem = await AssignmentRules.CheckTeachesAsync(db, teacherId.Value, command.SubjectId, classroomIds, ct);
        if (problem is not null)
        {
            return problem;
        }

        var termId = await db.Terms.AsNoTracking().Where(t => t.IsActive && t.AcademicYear!.IsActive).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (termId is null)
        {
            return Error.Conflict("assignments.no_active_term", "There is no current term. Ask the admin staff to set one.");
        }

        var attachments = command.Attachments ?? [];
        foreach (var file in attachments)
        {
            if (!AssignmentFiles.IsAttachmentKeyOf(file.Key, teacherId.Value))
            {
                return Error.Validation("files.invalid_key", "An attachment does not belong to you.");
            }

            var fileProblem = await AssignmentFiles.VerifyAsync(storage, file.Key, AssignmentFileRules.AttachmentTypes, ct);
            if (fileProblem is not null)
            {
                return fileProblem;
            }
        }

        var assignment = Assignment.Create(teacherId.Value, command.SubjectId, termId.Value, command.Title, command.Description ?? string.Empty, command.DueAt, classroomIds);
        foreach (var file in attachments)
        {
            assignment.AddAttachment(file.Key, file.FileName);
        }

        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        var subjectName = await db.Subjects.Where(s => s.Id == command.SubjectId).Select(s => s.Name).FirstAsync(ct);
        await AssignmentNotices.NewAssignmentAsync(db, notifications, clock, assignment.Id, assignment.Title, subjectName, assignment.DueAt, classroomIds, ct);

        return (await AssignmentLoader.LoadAsync(db, assignment.Id, canEdit: true, studentId: null, ct))!;
    }
}

internal static class AssignmentRules
{
    /// <summary>The teacher must teach the subject in every chosen classroom (timetable of the current year).</summary>
    public static async Task<Error?> CheckTeachesAsync(IAppDbContext db, Guid teacherId, Guid subjectId, IReadOnlyCollection<Guid> classroomIds, CancellationToken ct)
    {
        var taught = await ListTeachingOptionsHandler.TeachingPairs(db, teacherId)
            .Where(o => o.SubjectId == subjectId)
            .Select(o => o.ClassroomId)
            .ToListAsync(ct);

        return classroomIds.All(taught.Contains)
            ? null
            : Error.Forbidden("assignments.not_your_class", "You can only give assignments for subjects you teach in those classrooms.");
    }
}
