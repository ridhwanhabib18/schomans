using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Students.CreateStudent;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Students.ImportStudents;

public sealed class ImportStudentsHandler(
    IAppDbContext db,
    IStudentImportParser parser,
    IValidator<CreateStudentCommand> rowValidator,
    IValidator<ImportStudentsCommand> fileValidator)
{
    public async Task<Result<ImportStudentsResult>> Handle(ImportStudentsCommand command, CancellationToken ct)
    {
        var fileCheck = await fileValidator.ValidateAsync(command, ct);
        if (!fileCheck.IsValid)
        {
            return Error.Validation("students.import_invalid_file", string.Join(" ", fileCheck.Errors.Select(e => e.ErrorMessage)));
        }

        var parsed = parser.Parse(command.File);
        if (parsed.FileError is not null)
        {
            return Error.Validation("students.import_invalid_file", parsed.FileError);
        }

        // Students are placed in the classrooms of the active academic year, found by name.
        var classrooms = await db.Classrooms
            .AsNoTracking()
            .Where(c => c.AcademicYear!.IsActive)
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);

        var fileNisns = parsed.Rows.Select(r => r.Nisn.Trim()).Where(n => n.Length > 0).Distinct().ToList();
        var fileEmails = parsed.Rows.Select(r => User.NormalizeEmail(r.Email)).Where(e => e.Length > 0).Distinct().ToList();
        var takenNisns = (await db.Students.AsNoTracking().Where(s => fileNisns.Contains(s.Nisn)).Select(s => s.Nisn).ToListAsync(ct)).ToHashSet();
        var takenEmails = (await db.Users.AsNoTracking().Where(u => fileEmails.Contains(u.Email)).Select(u => u.Email).ToListAsync(ct)).ToHashSet();

        var failed = new List<ImportRowError>();
        var accepted = new List<(ParsedStudentRow Row, Guid ClassroomId)>();
        var seenNisns = new Dictionary<string, int>();
        var seenEmails = new Dictionary<string, int>();

        foreach (var row in parsed.Rows)
        {
            var problems = new List<string>();
            var nisn = row.Nisn.Trim();
            var email = User.NormalizeEmail(row.Email);

            var hasClassroom = classrooms.TryGetValue(row.Classroom.Trim(), out var classroomId);
            if (!hasClassroom)
            {
                problems.Add(string.IsNullOrWhiteSpace(row.Classroom)
                    ? "The classroom is missing."
                    : $"The classroom '{row.Classroom.Trim()}' does not exist in the active academic year.");
            }

            if (row.DateOfBirth is null)
            {
                problems.Add($"The date of birth '{row.DateOfBirthText}' is missing or not a valid date (use yyyy-MM-dd).");
            }

            // The same rules as the create form, so an imported row can never be worse than a hand-made one.
            var form = new CreateStudentCommand(row.FullName.Trim(), row.Email.Trim(), nisn, hasClassroom ? classroomId : Guid.NewGuid(), row.DateOfBirth ?? new DateOnly(2012, 1, 1));
            problems.AddRange((await rowValidator.ValidateAsync(form, ct)).Errors.Select(e => e.ErrorMessage));

            if (nisn.Length > 0 && takenNisns.Contains(nisn))
            {
                problems.Add($"The NISN {nisn} already belongs to another student.");
            }
            else if (nisn.Length > 0 && seenNisns.TryGetValue(nisn, out var firstNisnRow))
            {
                problems.Add($"The NISN {nisn} appears twice in the file (first in row {firstNisnRow}).");
            }

            if (email.Length > 0 && takenEmails.Contains(email))
            {
                problems.Add($"The email {email} already belongs to another account.");
            }
            else if (email.Length > 0 && seenEmails.TryGetValue(email, out var firstEmailRow))
            {
                problems.Add($"The email {email} appears twice in the file (first in row {firstEmailRow}).");
            }

            if (problems.Count > 0)
            {
                failed.Add(new ImportRowError(row.RowNumber, string.Join(" ", problems.Distinct())));
                continue;
            }

            seenNisns[nisn] = row.RowNumber;
            seenEmails[email] = row.RowNumber;
            accepted.Add((row, classroomId));
        }

        var created = await Save(accepted, failed, ct);
        return new ImportStudentsResult(parsed.Rows.Count, created, failed.OrderBy(f => f.Row).ToList());
    }

    private async Task<int> Save(List<(ParsedStudentRow Row, Guid ClassroomId)> accepted, List<ImportRowError> failed, CancellationToken ct)
    {
        foreach (var (row, classroomId) in accepted)
        {
            AddStudent(row, classroomId);
        }

        try
        {
            await db.SaveChangesAsync(ct);
            return accepted.Count;
        }
        catch (DbUpdateException)
        {
            // Somebody created one of these students while the file was processed. Save the rows one by one so only
            // the conflicting ones fail and the rest are still imported.
            db.ChangeTracker.Clear();
        }

        var created = 0;
        foreach (var (row, classroomId) in accepted)
        {
            AddStudent(row, classroomId);
            try
            {
                await db.SaveChangesAsync(ct);
                created++;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                failed.Add(new ImportRowError(row.RowNumber, "The NISN or email was taken by another record while importing."));
            }
        }

        return created;
    }

    private void AddStudent(ParsedStudentRow row, Guid classroomId)
    {
        var user = User.Create(row.Email, row.FullName, UserRole.Student);
        db.Users.Add(user);
        db.Students.Add(Student.Create(user.Id, row.Nisn, classroomId, row.DateOfBirth!.Value));
    }
}
