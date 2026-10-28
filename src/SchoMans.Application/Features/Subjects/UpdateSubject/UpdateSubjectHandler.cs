using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Subjects.UpdateSubject;

public sealed class UpdateSubjectHandler(IAppDbContext db)
{
    public async Task<Result<SubjectDto>> Handle(UpdateSubjectCommand command, CancellationToken ct)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (subject is null)
        {
            return Error.NotFound("subjects.not_found", "The subject does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var name = command.Name.Trim();
        if (await db.Subjects.AnyAsync(s => s.Name == name && s.Id != subject.Id, ct))
        {
            return Error.Conflict("subjects.name_taken", "A subject with this name already exists.");
        }

        subject.Update(name, command.MinimumPassingScore);
        db.SetExpectedRowVersion(subject, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("subjects.name_taken", "A subject with this name already exists.");
        }

        var row = await db.Subjects.AsNoTracking().Where(s => s.Id == subject.Id).Select(SubjectMapping.Projection).FirstAsync(ct);
        return SubjectMapping.ToDto(row);
    }
}
