using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.Periods;

public sealed record ListAdmissionPeriodsQuery;

public sealed record CreateAdmissionPeriodCommand(
    Guid AcademicYearId,
    string Name,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    DateTimeOffset ResultsAnnouncedAt,
    DateTimeOffset ReregistrationDeadline);

public sealed record UpdateAdmissionPeriodCommand(
    Guid Id,
    string Name,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    DateTimeOffset ResultsAnnouncedAt,
    DateTimeOffset ReregistrationDeadline);

public sealed record DeleteAdmissionPeriodCommand(Guid Id);

internal static class PeriodRules
{
    public static void Dates<T>(AbstractValidator<T> validator, Func<T, (DateTimeOffset Opens, DateTimeOffset Closes, DateTimeOffset Results, DateTimeOffset Deadline)> dates)
    {
        validator.RuleFor(x => x)
            .Must(x =>
            {
                var d = dates(x);
                return d.Opens < d.Closes && d.Closes < d.Results && d.Results < d.Deadline;
            })
            .WithName("Dates")
            .WithMessage("The dates must run in order: opens, closes, results announced, re-registration deadline.");
    }

    /// <summary>Two periods may not be open at the same time: "the open period" must be one.</summary>
    public static Task<bool> OverlapsAsync(IAppDbContext db, Guid? exceptId, DateTimeOffset opensAt, DateTimeOffset closesAt, CancellationToken ct) =>
        db.AdmissionPeriods.AnyAsync(p => p.Id != exceptId && p.OpensAt < closesAt && opensAt < p.ClosesAt, ct);

    public static Task<AdmissionPeriodDto> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        db.AdmissionPeriods.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new AdmissionPeriodDto(
                p.Id,
                p.Name,
                p.AcademicYearId,
                p.AcademicYear!.Name,
                p.OpensAt,
                p.ClosesAt,
                p.ResultsAnnouncedAt,
                p.ReregistrationDeadline,
                db.Applicants.Count(a => a.AdmissionPeriodId == p.Id)))
            .FirstAsync(ct);
}

public sealed class CreateAdmissionPeriodValidator : AbstractValidator<CreateAdmissionPeriodCommand>
{
    public CreateAdmissionPeriodValidator()
    {
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(AdmissionPeriod.NameMaxLength);
        PeriodRules.Dates(this, x => (x.OpensAt, x.ClosesAt, x.ResultsAnnouncedAt, x.ReregistrationDeadline));
    }
}

public sealed class UpdateAdmissionPeriodValidator : AbstractValidator<UpdateAdmissionPeriodCommand>
{
    public UpdateAdmissionPeriodValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(AdmissionPeriod.NameMaxLength);
        PeriodRules.Dates(this, x => (x.OpensAt, x.ClosesAt, x.ResultsAnnouncedAt, x.ReregistrationDeadline));
    }
}

public sealed class ListAdmissionPeriodsHandler(IAppDbContext db)
{
    public async Task<Result<List<AdmissionPeriodDto>>> Handle(ListAdmissionPeriodsQuery query, CancellationToken ct) =>
        await db.AdmissionPeriods.AsNoTracking()
            .OrderByDescending(p => p.OpensAt)
            .Select(p => new AdmissionPeriodDto(
                p.Id,
                p.Name,
                p.AcademicYearId,
                p.AcademicYear!.Name,
                p.OpensAt,
                p.ClosesAt,
                p.ResultsAnnouncedAt,
                p.ReregistrationDeadline,
                db.Applicants.Count(a => a.AdmissionPeriodId == p.Id)))
            .ToListAsync(ct);
}

public sealed class CreateAdmissionPeriodHandler(IAppDbContext db)
{
    public async Task<Result<AdmissionPeriodDto>> Handle(CreateAdmissionPeriodCommand command, CancellationToken ct)
    {
        if (!await db.AcademicYears.AnyAsync(y => y.Id == command.AcademicYearId, ct))
        {
            return Error.NotFound("academic_years.not_found", "The academic year does not exist.");
        }

        if (await PeriodRules.OverlapsAsync(db, null, command.OpensAt, command.ClosesAt, ct))
        {
            return Error.Conflict("admissions.period_overlaps", "Another admission period is open at the same time.");
        }

        var period = AdmissionPeriod.Create(command.AcademicYearId, command.Name, command.OpensAt, command.ClosesAt, command.ResultsAnnouncedAt, command.ReregistrationDeadline);
        db.AdmissionPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return await PeriodRules.LoadAsync(db, period.Id, ct);
    }
}

public sealed class UpdateAdmissionPeriodHandler(IAppDbContext db)
{
    public async Task<Result<AdmissionPeriodDto>> Handle(UpdateAdmissionPeriodCommand command, CancellationToken ct)
    {
        var period = await db.AdmissionPeriods.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (period is null)
        {
            return Error.NotFound("admissions.period_not_found", "The admission period does not exist.");
        }

        if (await PeriodRules.OverlapsAsync(db, period.Id, command.OpensAt, command.ClosesAt, ct))
        {
            return Error.Conflict("admissions.period_overlaps", "Another admission period is open at the same time.");
        }

        period.Update(command.Name, command.OpensAt, command.ClosesAt, command.ResultsAnnouncedAt, command.ReregistrationDeadline);
        await db.SaveChangesAsync(ct);
        return await PeriodRules.LoadAsync(db, period.Id, ct);
    }
}

public sealed class DeleteAdmissionPeriodHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteAdmissionPeriodCommand command, CancellationToken ct)
    {
        var period = await db.AdmissionPeriods.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (period is null)
        {
            return Error.NotFound("admissions.period_not_found", "The admission period does not exist.");
        }

        if (await db.Applicants.AnyAsync(a => a.AdmissionPeriodId == period.Id, ct))
        {
            return Error.Conflict("admissions.period_in_use", "This period has applications, so it cannot be deleted.");
        }

        db.AdmissionPeriods.Remove(period);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
