using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Subjects.CreateSubject;

public sealed class CreateSubjectHandler(IAppDbContext db)
{
    public async Task<Result<SubjectDto>> Handle(CreateSubjectCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        if (await db.Subjects.AnyAsync(s => s.Name == name, ct))
        {
            return Error.Conflict("subjects.name_taken", "A subject with this name already exists.");
        }

        var subject = Subject.Create(name, command.MinimumPassingScore);
        db.Subjects.Add(subject);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("subjects.name_taken", "A subject with this name already exists.");
        }

        var row = await db.Subjects.AsNoTracking().Where(s => s.Id == subject.Id).Select(SubjectMapping.Projection).FirstAsync(ct);
        return SubjectMapping.ToDto(row);
    }
}
