using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments.CreateAssignment;

namespace SchoMans.Application.Features.Assignments.UpdateAssignment;

/// <summary>
/// The teacher corrects an assignment: text, due date, attachments. Classrooms can change only while nobody handed in
/// work yet. Moving the due date does not change the late mark of work already handed in.
/// </summary>
public sealed class UpdateAssignmentHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, ISchoolClock clock)
{
    public async Task<Result<AssignmentDto>> Handle(UpdateAssignmentCommand command, CancellationToken ct)
    {
        var assignment = await db.Assignments.Include(a => a.Classrooms).Include(a => a.Attachments).FirstOrDefaultAsync(a => a.Id == command.Id, ct);
        if (assignment is null)
        {
            return Error.NotFound("assignments.not_found", "The assignment does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId != assignment.TeacherId)
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the assignment can change it.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        if (command.DueAt != assignment.DueAt && command.DueAt <= clock.UtcNow)
        {
            return Error.Validation("assignments.due_in_past", "The new due date must be in the future.");
        }

        var classroomIds = command.ClassroomIds.Distinct().ToList();
        var classroomsChanged = !classroomIds.ToHashSet().SetEquals(assignment.Classrooms.Select(c => c.ClassroomId));
        if (classroomsChanged)
        {
            if (await db.Submissions.AnyAsync(s => s.AssignmentId == assignment.Id, ct))
            {
                return Error.Conflict("assignments.has_submissions", "Students already handed in work, so the classrooms cannot change any more.");
            }

            var problem = await AssignmentRules.CheckTeachesAsync(db, teacherId.Value, assignment.SubjectId, classroomIds, ct);
            if (problem is not null)
            {
                return problem;
            }
        }

        foreach (var file in command.NewAttachments ?? [])
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

        assignment.UpdateDetails(command.Title, command.Description ?? string.Empty, command.DueAt);
        if (classroomsChanged)
        {
            assignment.SetClassrooms(classroomIds);
        }

        var keep = (command.KeepAttachmentIds ?? []).ToHashSet();
        var removed = assignment.Attachments.Where(a => !keep.Contains(a.Id)).Select(a => a.Id).ToList()
            .Select(assignment.RemoveAttachment)
            .OfType<Domain.Assignments.AssignmentAttachment>()
            .ToList();
        foreach (var file in command.NewAttachments ?? [])
        {
            assignment.AddAttachment(file.Key, file.FileName);
        }

        db.SetExpectedRowVersion(assignment, rowVersion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }

        // The files of removed attachments go too; the record is saved first, so a failed delete leaves only an orphan file.
        foreach (var file in removed)
        {
            await storage.DeleteAsync(FileBucket.Private, file.FileKey, ct);
        }

        return (await AssignmentLoader.LoadAsync(db, assignment.Id, canEdit: true, studentId: null, ct))!;
    }
}
