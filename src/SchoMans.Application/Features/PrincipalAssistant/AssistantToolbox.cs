using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.GradeWeights;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.PrincipalAssistant;

/// <summary>What a tool gives back: the JSON the model reads, and the source shown under the answer (null when the call failed).</summary>
internal sealed record ToolOutcome(string Content, AssistantSourceDto? Source);

/// <summary>
/// The query functions of the principal assistant (AI-04). The model never writes SQL: it can only call these, with arguments that
/// are checked here. Every result has a plain-language <c>summary</c>, and students are given as codes (S1, S2...) that the caller
/// turns back into names in the answer. The caller has already checked that the user is the principal.
/// </summary>
internal sealed class AssistantToolbox(IAppDbContext db, ISchoolClock clock)
{
    private const int DefaultDays = 30;
    private const int MaxListed = 10;
    private const decimal DefaultThreshold = 80m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string NoArguments = """{"type":"object","properties":{}}""";

    private const string ClassroomAndDates = """
        {"type":"object","properties":{
          "classroom":{"type":"string","description":"Part of a classroom name; leave out for all classrooms."},
          "from":{"type":"string","description":"First day, yyyy-MM-dd. Default: 30 days ago."},
          "to":{"type":"string","description":"Last day, yyyy-MM-dd. Default: today."}}}
        """;

    public static IReadOnlyList<AiTool> Definitions { get; } =
    [
        new(AssistantToolNames.ListClassrooms, "The classrooms of the school with their number of active students.", NoArguments),
        new(AssistantToolNames.AttendanceByClassroom, "Attendance per classroom over a period: present, excused, sick, absent and the attendance rate (present / all records).", ClassroomAndDates),
        new(AssistantToolNames.AttendanceTrend, "The attendance rate per week, for the whole school or one classroom.", """
            {"type":"object","properties":{
              "classroom":{"type":"string","description":"Part of a classroom name; leave out for the whole school."},
              "weeks":{"type":"integer","description":"How many weeks back, 1 to 12. Default 6."}}}
            """),
        new(AssistantToolNames.StudentsLowAttendance, "Students whose attendance this term is below a threshold, worst first (at most 10 are named).", """
            {"type":"object","properties":{
              "classroom":{"type":"string","description":"Part of a classroom name; leave out for all classrooms."},
              "threshold":{"type":"number","description":"Attendance rate in percent. Default 80."}}}
            """),
        new(AssistantToolNames.AverageGradesBySubject, "The average final grade per subject in the active term, and how many students are below the minimum passing score.", """
            {"type":"object","properties":{
              "classroom":{"type":"string","description":"Part of a classroom name; leave out for all classrooms."}}}
            """),
        new(AssistantToolNames.PendingRequests, "Requests (leave, certificate letters) that wait for an approval: how many per type and how long the oldest has waited.", NoArguments),
        new(AssistantToolNames.AdmissionApplicants, "The applicants of the current or latest admission period, counted per status.", NoArguments),
        new(AssistantToolNames.AssignmentSummary, "Assignments due in a period: how many were handed in on time, late, or not at all.", ClassroomAndDates),
    ];

    public async Task<ToolOutcome> ExecuteAsync(string name, string arguments, NameAliases aliases, CancellationToken ct)
    {
        JsonElement args;
        try
        {
            args = string.IsNullOrWhiteSpace(arguments) ? default : JsonSerializer.Deserialize<JsonElement>(arguments);
        }
        catch (JsonException)
        {
            return Fail("The arguments are not valid JSON.");
        }

        try
        {
            return name switch
            {
                AssistantToolNames.ListClassrooms => await ListClassroomsAsync(ct),
                AssistantToolNames.AttendanceByClassroom => await AttendanceByClassroomAsync(args, ct),
                AssistantToolNames.AttendanceTrend => await AttendanceTrendAsync(args, ct),
                AssistantToolNames.StudentsLowAttendance => await StudentsLowAttendanceAsync(args, aliases, ct),
                AssistantToolNames.AverageGradesBySubject => await AverageGradesAsync(args, ct),
                AssistantToolNames.PendingRequests => await PendingRequestsAsync(ct),
                AssistantToolNames.AdmissionApplicants => await AdmissionApplicantsAsync(ct),
                AssistantToolNames.AssignmentSummary => await AssignmentSummaryAsync(args, ct),
                _ => Fail($"There is no function named {name}."),
            };
        }
        catch (ArgumentException exception)
        {
            return Fail(exception.Message);
        }
    }

    // ---- the tools ----

    private async Task<ToolOutcome> ListClassroomsAsync(CancellationToken ct)
    {
        var students = (await db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active).GroupBy(s => s.ClassroomId)
                .Select(g => new { ClassroomId = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(g => g.ClassroomId, g => g.Count);
        var classrooms = (await db.Classrooms.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
            .Select(c => new { name = c.Name, students = students.GetValueOrDefault(c.Id) }).ToList();

        var summary = $"{classrooms.Count} classrooms with {classrooms.Sum(c => c.students)} active students in total.";
        return Ok(new { summary, classrooms }, "Classrooms", "/manage/classrooms", [("Classrooms", classrooms.Count.ToString(CultureInfo.InvariantCulture)), ("Active students", classrooms.Sum(c => c.students).ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ToolOutcome> AttendanceByClassroomAsync(JsonElement args, CancellationToken ct)
    {
        var (from, to) = Period(args);
        var classroom = await FindClassroomAsync(args, ct);
        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Session!.Date >= from && r.Session.Date <= to && (classroom == null || r.Student!.ClassroomId == classroom.Id))
            .GroupBy(r => new { r.Student!.ClassroomId, r.Status })
            .Select(g => new { g.Key.ClassroomId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var names = await db.Classrooms.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var perClassroom = rows.GroupBy(r => r.ClassroomId).Select(g =>
        {
            int Of(AttendanceStatus status) => g.Where(r => r.Status == status).Sum(r => r.Count);
            var total = g.Sum(r => r.Count);
            return new
            {
                classroom = names.GetValueOrDefault(g.Key, "?"),
                present = Of(AttendanceStatus.Present),
                excused = Of(AttendanceStatus.Excused),
                sick = Of(AttendanceStatus.Sick),
                absent = Of(AttendanceStatus.Absent),
                records = total,
                ratePercent = Rate(Of(AttendanceStatus.Present), total),
            };
        }).OrderBy(c => c.classroom, StringComparer.Ordinal).ToList();

        var allPresent = perClassroom.Sum(c => c.present);
        var all = perClassroom.Sum(c => c.records);
        var summary = all == 0
            ? $"No attendance was recorded from {Day(from)} to {Day(to)}."
            : $"From {Day(from)} to {Day(to)} the attendance rate is {Rate(allPresent, all)}% over {all} records in {perClassroom.Count} classroom(s).";
        return Ok(new { from = Day(from), to = Day(to), summary, classrooms = perClassroom }, "Attendance per classroom", "/attendance/reports",
            [("Period", $"{Day(from)} to {Day(to)}"), ("Records", all.ToString(CultureInfo.InvariantCulture)), ("Attendance rate", all == 0 ? "n/a" : $"{Rate(allPresent, all)}%")]);
    }

    private async Task<ToolOutcome> AttendanceTrendAsync(JsonElement args, CancellationToken ct)
    {
        var weeks = Math.Clamp(Int(args, "weeks") ?? 6, 1, 12);
        var classroom = await FindClassroomAsync(args, ct);
        var thisMonday = clock.Today.AddDays(-(((int)clock.Today.DayOfWeek + 6) % 7));
        var from = thisMonday.AddDays(-7 * (weeks - 1));

        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Session!.Date >= from && (classroom == null || r.Student!.ClassroomId == classroom.Id))
            .GroupBy(r => new { r.Session!.Date, r.Status })
            .Select(g => new { g.Key.Date, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var points = Enumerable.Range(0, weeks).Select(i =>
        {
            var start = from.AddDays(7 * i);
            var inWeek = rows.Where(r => r.Date >= start && r.Date < start.AddDays(7)).ToList();
            var total = inWeek.Sum(r => r.Count);
            return new { weekStarting = Day(start), records = total, ratePercent = Rate(inWeek.Where(r => r.Status == AttendanceStatus.Present).Sum(r => r.Count), total) };
        }).ToList();

        var withData = points.Where(p => p.ratePercent is not null).ToList();
        var summary = withData.Count == 0
            ? "No attendance was recorded in these weeks."
            : $"The weekly attendance rate went from {withData[0].ratePercent}% (week of {withData[0].weekStarting}) to {withData[^1].ratePercent}% (week of {withData[^1].weekStarting}).";
        return Ok(new { classroom = classroom?.Name ?? "all classrooms", summary, weeks = points }, "Attendance trend", "/attendance/reports",
            [("Scope", classroom?.Name ?? "All classrooms"), ("Weeks", weeks.ToString(CultureInfo.InvariantCulture)), ("Latest week", withData.Count == 0 ? "n/a" : $"{withData[^1].ratePercent}%")]);
    }

    private async Task<ToolOutcome> StudentsLowAttendanceAsync(JsonElement args, NameAliases aliases, CancellationToken ct)
    {
        var threshold = Number(args, "threshold") ?? DefaultThreshold;
        var classroom = await FindClassroomAsync(args, ct);
        var term = await db.Terms.AsNoTracking().Where(t => t.IsActive).Select(t => new { t.Name, t.StartDate, t.EndDate }).FirstOrDefaultAsync(ct);
        if (term is null)
        {
            return Fail("There is no active term.");
        }

        var to = term.EndDate < clock.Today ? term.EndDate : clock.Today;
        var counts = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Session!.Date >= term.StartDate && r.Session.Date <= to && r.Student!.Status == StudentStatus.Active
                && (classroom == null || r.Student.ClassroomId == classroom.Id))
            .GroupBy(r => new { r.StudentId, r.Status })
            .Select(g => new { g.Key.StudentId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var flagged = counts.GroupBy(c => c.StudentId)
            .Select(g => new { StudentId = g.Key, Total = g.Sum(c => c.Count), Rate = Rate(g.Where(c => c.Status == AttendanceStatus.Present).Sum(c => c.Count), g.Sum(c => c.Count)) })
            .Where(s => s.Rate < threshold)
            .OrderBy(s => s.Rate)
            .ToList();

        var shown = flagged.Take(MaxListed).Select(s => s.StudentId).ToList();
        var people = await db.Students.AsNoTracking().Where(s => shown.Contains(s.Id))
            .Select(s => new { s.Id, Name = s.User!.FullName, Classroom = s.Classroom!.Name }).ToListAsync(ct);
        var listed = flagged.Take(MaxListed).Select(s =>
        {
            var person = people.First(p => p.Id == s.StudentId);
            return new { student = aliases.For(s.StudentId, person.Name), classroom = person.Classroom, ratePercent = s.Rate, records = s.Total };
        }).ToList();

        var summary = flagged.Count == 0
            ? $"No student is below {threshold}% attendance in {term.Name}."
            : $"{flagged.Count} student(s) are below {threshold}% attendance in {term.Name}; the lowest is {listed[0].student} with {listed[0].ratePercent}%.";
        return Ok(new { term = term.Name, thresholdPercent = threshold, summary, totalBelowThreshold = flagged.Count, students = listed }, "Students with low attendance", "/dashboard",
            [("Term", term.Name), ("Threshold", $"{threshold}%"), ("Students below", flagged.Count.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ToolOutcome> AverageGradesAsync(JsonElement args, CancellationToken ct)
    {
        var classroom = await FindClassroomAsync(args, ct);
        var term = await db.Terms.AsNoTracking().Where(t => t.IsActive).Select(t => new { t.Id, t.Name }).FirstOrDefaultAsync(ct);
        if (term is null)
        {
            return Fail("There is no active term.");
        }

        var grades = await db.Grades.AsNoTracking()
            .Where(g => g.TermId == term.Id && (classroom == null || g.Student!.ClassroomId == classroom.Id))
            .Select(g => new { g.StudentId, g.SubjectId, g.Component, g.Score })
            .ToListAsync(ct);
        var subjects = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => new { s.Name, s.MinimumPassingScore }, ct);

        var rows = new List<object>();
        var counted = 0;
        foreach (var subject in grades.GroupBy(g => g.SubjectId).Where(g => subjects.ContainsKey(g.Key)).OrderBy(g => subjects[g.Key].Name, StringComparer.Ordinal))
        {
            var weights = (await GradeWeightsLoader.LoadAsync(db, subject.Key, ct)).Weights.ToDictionary(w => w.Component, w => w.WeightPercent);
            var finals = subject.GroupBy(g => g.StudentId)
                .Select(s => GradeCalculator.FinalGrade(weights, s.Select(g => (g.Component, g.Score))))
                .Where(f => f is not null).Select(f => f!.Value).ToList();
            if (finals.Count == 0)
            {
                continue;
            }

            var info = subjects[subject.Key];
            counted += finals.Count;
            rows.Add(new
            {
                subject = info.Name,
                averageFinalGrade = decimal.Round(finals.Average(), 1),
                students = finals.Count,
                belowMinimum = finals.Count(f => f < info.MinimumPassingScore),
                minimumPassingScore = info.MinimumPassingScore,
            });
        }

        var summary = rows.Count == 0
            ? $"No grades are entered in {term.Name}."
            : $"Average final grades in {term.Name} for {rows.Count} subject(s), {counted} student results in total.";
        return Ok(new { term = term.Name, classroom = classroom?.Name ?? "all classrooms", summary, subjects = rows }, "Average grades per subject", "/dashboard",
            [("Term", term.Name), ("Scope", classroom?.Name ?? "All classrooms"), ("Subjects", rows.Count.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ToolOutcome> PendingRequestsAsync(CancellationToken ct)
    {
        var pending = await db.Requests.AsNoTracking().Where(r => r.Status == RequestStatus.Pending)
            .Select(r => new { r.Type, r.CreatedAt }).ToListAsync(ct);
        var now = clock.UtcNow;
        var byType = pending.GroupBy(r => r.Type).OrderBy(g => g.Key)
            .Select(g => new { type = g.Key.ToString(), count = g.Count(), oldestWaitingDays = (int)(now - g.Min(r => r.CreatedAt)).TotalDays }).ToList();

        var summary = pending.Count == 0
            ? "No request is waiting for an approval."
            : $"{pending.Count} request(s) wait for an approval; the oldest has waited {byType.Max(t => t.oldestWaitingDays)} day(s).";
        return Ok(new { summary, total = pending.Count, byType }, "Pending requests", "/manage/requests",
            [("Waiting", pending.Count.ToString(CultureInfo.InvariantCulture)), ("Oldest (days)", pending.Count == 0 ? "0" : byType.Max(t => t.oldestWaitingDays).ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ToolOutcome> AdmissionApplicantsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var period = await db.AdmissionPeriods.AsNoTracking()
            .OrderByDescending(p => p.OpensAt <= now && p.ClosesAt >= now).ThenByDescending(p => p.OpensAt)
            .Select(p => new { p.Id, p.Name }).FirstOrDefaultAsync(ct);
        if (period is null)
        {
            return Fail("There is no admission period.");
        }

        var counts = await db.Applicants.AsNoTracking().Where(a => a.AdmissionPeriodId == period.Id)
            .GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var byStatus = Enum.GetValues<ApplicantStatus>().Select(s => new { status = s.ToString(), count = counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0 }).ToList();
        var total = byStatus.Sum(s => s.count);

        var summary = $"{total} applicant(s) in \"{period.Name}\": " + string.Join(", ", byStatus.Where(s => s.count > 0).Select(s => $"{s.count} {s.status}")) + ".";
        return Ok(new { period = period.Name, summary, total, byStatus }, "Admission applicants", null,
            [("Period", period.Name), ("Applicants", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ToolOutcome> AssignmentSummaryAsync(JsonElement args, CancellationToken ct)
    {
        var (from, to) = Period(args);
        var classroom = await FindClassroomAsync(args, ct);
        var fromUtc = clock.ToUtc(from, TimeOnly.MinValue);
        var untilUtc = clock.ToUtc(to.AddDays(1), TimeOnly.MinValue);

        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.DueAt >= fromUtc && a.DueAt < untilUtc && (classroom == null || a.Classrooms.Any(c => c.ClassroomId == classroom.Id)))
            .Select(a => new { a.Id, ClassroomIds = a.Classrooms.Select(c => c.ClassroomId).ToList() })
            .ToListAsync(ct);
        var ids = assignments.Select(a => a.Id).ToList();

        var students = (await db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active).GroupBy(s => s.ClassroomId)
                .Select(g => new { ClassroomId = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(g => g.ClassroomId, g => g.Count);
        var submitted = await db.Submissions.AsNoTracking()
            .Where(s => ids.Contains(s.AssignmentId) && (classroom == null || s.Student!.ClassroomId == classroom.Id))
            .Select(s => new { s.IsLate }).ToListAsync(ct);

        var expected = assignments.Sum(a => a.ClassroomIds.Where(id => classroom is null || id == classroom.Id).Sum(id => students.GetValueOrDefault(id)));
        var late = submitted.Count(s => s.IsLate);
        var onTime = submitted.Count - late;
        var missing = Math.Max(0, expected - submitted.Count);

        var summary = assignments.Count == 0
            ? $"No assignment was due from {Day(from)} to {Day(to)}."
            : $"{assignments.Count} assignment(s) were due from {Day(from)} to {Day(to)}: {onTime} handed in on time, {late} late, {missing} not handed in (of {expected} expected).";
        return Ok(new { from = Day(from), to = Day(to), classroom = classroom?.Name ?? "all classrooms", summary, assignments = assignments.Count, expected, onTime, late, notHandedIn = missing }, "Assignments", "/assignments",
            [("Period", $"{Day(from)} to {Day(to)}"), ("Assignments", assignments.Count.ToString(CultureInfo.InvariantCulture)), ("Not handed in", missing.ToString(CultureInfo.InvariantCulture))]);
    }

    // ---- helpers ----

    private static ToolOutcome Ok(object result, string label, string? url, (string Label, string Value)[] figures) =>
        new(JsonSerializer.Serialize(result, JsonOptions), new AssistantSourceDto(string.Empty, label, url, figures.Select(f => new AssistantFigureDto(f.Label, f.Value)).ToList()));

    private static ToolOutcome Fail(string message) => new(JsonSerializer.Serialize(new { error = message }, JsonOptions), null);

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal? Rate(int present, int total) => total == 0 ? null : decimal.Round(present * 100m / total, 1);

    private (DateOnly From, DateOnly To) Period(JsonElement args)
    {
        var to = Date(args, "to") ?? clock.Today;
        var from = Date(args, "from") ?? to.AddDays(-DefaultDays);
        if (from > to)
        {
            throw new ArgumentException("The start of the period is after its end.");
        }

        return (from, to);
    }

    private static DateOnly? Date(JsonElement args, string name)
    {
        var text = Text(args, name);
        if (text is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ArgumentException($"'{name}' must be a date like 2026-10-31.");
    }

    private static string? Text(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static int? Int(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static decimal? Number(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) ? number : null;

    private async Task<ClassroomRef?> FindClassroomAsync(JsonElement args, CancellationToken ct)
    {
        var text = Text(args, "classroom");
        if (text is null)
        {
            return null;
        }

        var matches = await db.Classrooms.AsNoTracking().Where(c => c.Name.Contains(text)).OrderBy(c => c.Name).Select(c => new ClassroomRef(c.Id, c.Name)).Take(2).ToListAsync(ct);
        return matches.Count switch
        {
            0 => throw new ArgumentException($"No classroom has '{text}' in its name; call {AssistantToolNames.ListClassrooms} to see the names."),
            1 => matches[0],
            _ => matches.FirstOrDefault(m => string.Equals(m.Name, text, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"More than one classroom matches '{text}'; give the full name."),
        };
    }

    private sealed record ClassroomRef(Guid Id, string Name);
}
