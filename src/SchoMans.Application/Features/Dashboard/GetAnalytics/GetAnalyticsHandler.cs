using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Dashboard.GetAnalytics;

public sealed record GetAnalyticsQuery;

/// <summary>
/// F-09: the principal sees the attendance trend per classroom and the average grade per subject, a teacher the students
/// who need attention, a parent or student the monthly grades and attendance of the child. Admin and applicants get nothing.
/// </summary>
public sealed class GetAnalyticsHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public const int TrendMonths = 6;
    public const decimal AttendanceThreshold = 80m;

    public async Task<Result<AnalyticsDto>> Handle(GetAnalyticsQuery query, CancellationToken ct)
    {
        var role = currentUser.Role;
        return role switch
        {
            UserRole.Principal => new AnalyticsDto(role.ToString(), await PrincipalAsync(ct), null, []),
            UserRole.Teacher => new AnalyticsDto(role.ToString(), null, await TeacherAsync(ct), []),
            UserRole.Student or UserRole.Parent => new AnalyticsDto(role.ToString(), null, null, await ChildrenAsync(ct)),
            _ => new AnalyticsDto(role.ToString(), null, null, []),
        };
    }

    private List<string> LastMonths()
    {
        var first = new DateOnly(clock.Today.Year, clock.Today.Month, 1).AddMonths(-(TrendMonths - 1));
        return Enumerable.Range(0, TrendMonths).Select(i => MonthKey(first.AddMonths(i))).ToList();
    }

    private static string MonthKey(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private DateOnly TrendStart() => new DateOnly(clock.Today.Year, clock.Today.Month, 1).AddMonths(-(TrendMonths - 1));

    private static decimal? Rate(int present, int total) => total == 0 ? null : decimal.Round(present * 100m / total, 1);

    private async Task<PrincipalAnalyticsDto> PrincipalAsync(CancellationToken ct)
    {
        var months = LastMonths();
        var from = TrendStart();

        var classrooms = await db.Classrooms.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        var counts = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Session!.Date >= from)
            .GroupBy(r => new { r.Student!.ClassroomId, r.Session!.Date, r.Status })
            .Select(g => new { g.Key.ClassroomId, g.Key.Date, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var trend = classrooms.Select(classroom =>
        {
            var own = counts.Where(c => c.ClassroomId == classroom.Id).ToList();
            var points = months.Select(month =>
            {
                var inMonth = own.Where(c => MonthKey(c.Date) == month).ToList();
                return new MonthRateDto(month, Rate(inMonth.Where(c => c.Status == AttendanceStatus.Present).Sum(c => c.Count), inMonth.Sum(c => c.Count)));
            }).ToList();
            return new ClassroomAttendanceTrendDto(classroom.Id, classroom.Name, points);
        }).ToList();

        return new PrincipalAnalyticsDto(trend, await SubjectAveragesAsync(ct));
    }

    private async Task<List<SubjectAverageDto>> SubjectAveragesAsync(CancellationToken ct)
    {
        var termId = await db.Terms.AsNoTracking().Where(t => t.IsActive).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (termId is null)
        {
            return [];
        }

        var grades = await db.Grades.AsNoTracking()
            .Where(g => g.TermId == termId)
            .Select(g => new { g.StudentId, g.SubjectId, g.Component, g.Score })
            .ToListAsync(ct);
        var weights = await LoadWeightsAsync(ct);
        var subjectNames = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return grades
            .GroupBy(g => g.SubjectId)
            .Select(subject =>
            {
                var finals = subject
                    .GroupBy(g => g.StudentId)
                    .Select(student => GradeCalculator.FinalGrade(WeightsOf(weights, subject.Key), student.Select(g => (g.Component, g.Score))))
                    .Where(f => f is not null)
                    .Select(f => f!.Value)
                    .ToList();
                return new { subject.Key, Finals = finals };
            })
            .Where(s => s.Finals.Count > 0 && subjectNames.ContainsKey(s.Key))
            .Select(s => new SubjectAverageDto(s.Key, subjectNames[s.Key], decimal.Round(s.Finals.Average(), 2), s.Finals.Count))
            .OrderBy(s => s.SubjectName)
            .ToList();
    }

    private async Task<TeacherAnalyticsDto> TeacherAsync(CancellationToken ct)
    {
        var empty = new TeacherAnalyticsDto(AttendanceThreshold, []);
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        var term = await db.Terms.AsNoTracking().Where(t => t.IsActive).Select(t => new { t.Id, t.StartDate, t.EndDate }).FirstOrDefaultAsync(ct);
        if (teacherId is null || term is null)
        {
            return empty;
        }

        var classroomIds = await SchoolMembership.TeacherClassroomIds(db, teacherId.Value).ToListAsync(ct);
        var homeroomIds = await db.Classrooms.Where(c => c.HomeroomTeacherId == teacherId).Select(c => c.Id).ToListAsync(ct);
        var taught = (await db.Schedules.AsNoTracking().Where(s => s.TeacherId == teacherId).Select(s => new { s.ClassroomId, s.SubjectId }).Distinct().ToListAsync(ct))
            .ToLookup(s => s.ClassroomId, s => s.SubjectId);

        var students = await db.Students.AsNoTracking()
            .Where(s => classroomIds.Contains(s.ClassroomId) && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, s.User!.FullName, s.ClassroomId, ClassroomName = s.Classroom!.Name })
            .ToListAsync(ct);
        var studentIds = students.Select(s => s.Id).ToList();

        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(r => studentIds.Contains(r.StudentId) && r.Session!.Date >= term.StartDate && r.Session.Date <= term.EndDate)
            .GroupBy(r => new { r.StudentId, r.Status })
            .Select(g => new { g.Key.StudentId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var grades = await db.Grades.AsNoTracking()
            .Where(g => g.TermId == term.Id && studentIds.Contains(g.StudentId))
            .Select(g => new { g.StudentId, g.SubjectId, g.Component, g.Score })
            .ToListAsync(ct);
        var weights = await LoadWeightsAsync(ct);
        var subjects = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => new { s.Name, s.MinimumPassingScore }, ct);

        var flagged = new List<AttentionStudentDto>();
        foreach (var student in students)
        {
            var own = attendance.Where(a => a.StudentId == student.Id).ToList();
            var rate = Rate(own.Where(a => a.Status == AttendanceStatus.Present).Sum(a => a.Count), own.Sum(a => a.Count));
            var lowAttendance = rate < AttendanceThreshold;

            // A homeroom teacher answers for every subject of the class; a subject teacher for their own.
            var visibleSubjects = homeroomIds.Contains(student.ClassroomId) ? null : taught[student.ClassroomId].ToHashSet();
            var below = grades
                .Where(g => g.StudentId == student.Id && (visibleSubjects is null || visibleSubjects.Contains(g.SubjectId)))
                .GroupBy(g => g.SubjectId)
                .Where(g => subjects.ContainsKey(g.Key))
                .Select(g => new { Subject = subjects[g.Key], Final = GradeCalculator.FinalGrade(WeightsOf(weights, g.Key), g.Select(x => (x.Component, x.Score))) })
                .Where(x => x.Final < x.Subject.MinimumPassingScore)
                .Select(x => x.Subject.Name)
                .Order()
                .ToList();

            if (lowAttendance || below.Count > 0)
            {
                flagged.Add(new AttentionStudentDto(student.Id, student.FullName, student.ClassroomName, rate, lowAttendance, below));
            }
        }

        return new TeacherAnalyticsDto(AttendanceThreshold, flagged.OrderBy(f => f.ClassroomName).ThenBy(f => f.StudentName).ToList());
    }

    private async Task<List<ChildAnalyticsDto>> ChildrenAsync(CancellationToken ct)
    {
        var childIds = currentUser.Role == UserRole.Student
            ? (await SchoolMembership.StudentIdAsync(db, currentUser, ct) is { } own ? [own] : [])
            : await SchoolMembership.ChildIdsAsync(db, currentUser, ct);

        var months = LastMonths();
        var from = TrendStart();
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var result = new List<ChildAnalyticsDto>();

        foreach (var childId in childIds)
        {
            var child = await db.Students.AsNoTracking().Where(s => s.Id == childId).Select(s => new { s.User!.FullName, s.ClassroomId }).FirstOrDefaultAsync(ct);
            if (child is null)
            {
                continue;
            }

            var publishedTerms = await db.GradePublications.AsNoTracking().Where(p => p.ClassroomId == child.ClassroomId).Select(p => p.TermId).ToListAsync(ct);
            var scores = await db.Grades.AsNoTracking()
                .Where(g => g.StudentId == childId && publishedTerms.Contains(g.TermId) && g.CreatedAt >= fromUtc)
                .Select(g => new { g.CreatedAt, g.Score })
                .ToListAsync(ct);
            var attendance = await db.AttendanceRecords.AsNoTracking()
                .Where(r => r.StudentId == childId && r.Session!.Date >= from)
                .Select(r => new { r.Session!.Date, r.Status })
                .ToListAsync(ct);

            var points = months.Select(month =>
            {
                var monthScores = scores.Where(s => MonthKey(DateOnly.FromDateTime(s.CreatedAt.UtcDateTime)) == month).Select(s => s.Score).ToList();
                var monthAttendance = attendance.Where(a => MonthKey(a.Date) == month).ToList();
                return new ChildMonthDto(
                    month,
                    monthScores.Count == 0 ? null : decimal.Round(monthScores.Average(), 2),
                    Rate(monthAttendance.Count(a => a.Status == AttendanceStatus.Present), monthAttendance.Count));
            }).ToList();

            result.Add(new ChildAnalyticsDto(childId, child.FullName, points));
        }

        return result;
    }

    private async Task<Dictionary<Guid, Dictionary<GradeComponent, decimal>>> LoadWeightsAsync(CancellationToken ct) =>
        (await db.GradeWeights.AsNoTracking().Select(w => new { w.SubjectId, w.Component, w.WeightPercent }).ToListAsync(ct))
            .GroupBy(w => w.SubjectId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(w => w.Component, w => w.WeightPercent));

    private static IReadOnlyDictionary<GradeComponent, decimal> WeightsOf(Dictionary<Guid, Dictionary<GradeComponent, decimal>> stored, Guid subjectId) =>
        stored.TryGetValue(subjectId, out var own) ? own : GradeCalculator.DefaultWeights;
}
