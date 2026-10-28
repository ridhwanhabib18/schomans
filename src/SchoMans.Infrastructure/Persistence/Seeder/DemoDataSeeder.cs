using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Seeder;

/// <summary>
/// Fills an empty database with one fictional school: one academic year, 5 classrooms, 15 teachers, 150 students and
/// 8 subjects with a weekly timetable, one login-ready account per role, some news and announcements, and unpublished
/// grades for the active term. Every name and number is made up;
/// nothing here is real personal data. Each part is seeded only when its tables are empty, so running it again (or on
/// a database that already holds an earlier part) never duplicates anything.
/// The demo accounts have no Keycloak id yet; the first login links them by email (see SyncUserHandler).
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, TimeProvider timeProvider, ILogger<DemoDataSeeder> logger)
{
    public const string DemoDomain = "schomans.demo";

    private const int ClassroomCount = 5;
    private const int TeacherCount = 15;
    private const int StudentsPerClassroom = 30;

    private static readonly string[] FirstNames =
    [
        "Ahmad", "Budi", "Citra", "Dewi", "Eko", "Fitri", "Gita", "Hadi", "Indah", "Joko",
        "Kartika", "Lestari", "Maya", "Nadia", "Oka", "Putri", "Qori", "Rizky", "Sinta", "Tono",
        "Umar", "Vina", "Wulan", "Xena", "Yoga", "Zahra", "Bayu", "Cahya", "Dimas", "Elok",
    ];

    private static readonly string[] LastNames =
    [
        "Santoso", "Wijaya", "Pratama", "Lestari", "Hidayat", "Saputra", "Kusuma", "Nugroho", "Rahayu", "Susanto",
        "Permana", "Maharani", "Setiawan", "Utami", "Firmansyah",
    ];

    private static readonly (string Name, decimal MinimumPassingScore)[] Subjects =
    [
        ("Mathematics", 75), ("Indonesian Language", 75), ("English", 75), ("Natural Sciences", 70),
        ("Social Studies", 70), ("Arts and Culture", 70), ("Physical Education", 70), ("Civics", 75),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedSchoolAsync(ct);
        await SeedSubjectsAsync(ct);
        await SeedSchedulesAsync(ct);
        await SeedCommunicationAsync(ct);
        await SeedGradesAsync(ct);
    }

    /// <summary>
    /// A quiz and a midterm score in every subject of the timetable for each student of the active term, so the grade
    /// book and the dashboards have something to show. A few scores fall below the minimum passing score on purpose.
    /// Nothing is published: the homeroom teacher publishes during the demo.
    /// </summary>
    private async Task SeedGradesAsync(CancellationToken ct)
    {
        // Grades of scored assignments do not count: the AddGrading migration creates them from earlier work, and that
        // must not keep an existing demo database from getting its demo grades.
        if (await db.Grades.IgnoreQueryFilters().AnyAsync(g => g.SubmissionId == null, ct))
        {
            return;
        }

        var termId = await db.Terms.Where(t => t.IsActive).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (termId is null)
        {
            return;
        }

        var lessons = (await db.Schedules.Select(s => new { s.ClassroomId, s.SubjectId }).Distinct().ToListAsync(ct))
            .ToLookup(s => s.ClassroomId, s => s.SubjectId);
        var students = await db.Students.Where(s => s.Status == StudentStatus.Active).OrderBy(s => s.Nisn).Select(s => new { s.Id, s.ClassroomId }).ToListAsync(ct);

        var count = 0;
        for (var i = 0; i < students.Count; i++)
        {
            var subjects = lessons[students[i].ClassroomId].Order().ToList();
            for (var j = 0; j < subjects.Count; j++)
            {
                // Deterministic and spread between 55 and 99.
                db.Grades.Add(Grade.Create(students[i].Id, subjects[j], termId.Value, GradeComponent.Quiz, 55 + ((i * 7) + (j * 11)) % 45));
                db.Grades.Add(Grade.Create(students[i].Id, subjects[j], termId.Value, GradeComponent.Midterm, 60 + ((i * 13) + (j * 5)) % 40));
                count += 2;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} demo grades", count);
    }

    private async Task SeedSchoolAsync(CancellationToken ct)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("School data not seeded: the database already contains users");
            return;
        }

        var year = AcademicYear.Create("2026/2027", new DateOnly(2026, 7, 13), new DateOnly(2027, 6, 19), isActive: true);
        db.AcademicYears.Add(year);
        db.Terms.Add(Term.Create(year.Id, "Term 1", new DateOnly(2026, 7, 13), new DateOnly(2026, 12, 19), isActive: true));
        db.Terms.Add(Term.Create(year.Id, "Term 2", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 19), isActive: false));

        var teachers = SeedTeachers();
        var classrooms = new List<Classroom>();
        for (var c = 0; c < ClassroomCount; c++)
        {
            // The first five teachers are homeroom teachers; the demo teacher is the homeroom teacher of class A.
            var classroom = Classroom.Create($"Class {(char)('A' + c)}", year.Id, teachers[c].Id);
            classrooms.Add(classroom);
            db.Classrooms.Add(classroom);
        }

        var students = SeedStudents(classrooms);

        // One account for each remaining role. The parent has two children: the demo student and the next student.
        AddUser("principal", "Hendra Wirawan", UserRole.Principal);
        AddUser("admin", "Sari Utami", UserRole.Admin);
        AddUser("applicant", "Dewi Lestari", UserRole.Applicant);
        var parent = AddUser("parent", "Agus Putra", UserRole.Parent);
        db.StudentParents.Add(StudentParent.Create(parent.Id, students[0].Id));
        db.StudentParents.Add(StudentParent.Create(parent.Id, students[1].Id));

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded demo school: {Teachers} teachers, {Classrooms} classrooms, {Students} students",
            teachers.Count,
            classrooms.Count,
            students.Count);
    }

    private async Task SeedSubjectsAsync(CancellationToken ct)
    {
        if (await db.Subjects.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        db.Subjects.AddRange(Subjects.Select(s => Subject.Create(s.Name, s.MinimumPassingScore)));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} subjects", Subjects.Length);
    }

    /// <summary>
    /// A Monday to Friday timetable for every classroom of the current year: four lessons a day. Each subject has one
    /// teacher (the demo teacher teaches Mathematics), and at any time the classrooms have different subjects, so no
    /// teacher is ever in two places at once.
    /// </summary>
    private async Task SeedSchedulesAsync(CancellationToken ct)
    {
        if (await db.Schedules.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var classrooms = await db.Classrooms.Where(c => c.AcademicYear!.IsActive).OrderBy(c => c.Name).ToListAsync(ct);
        var teachers = await db.Teachers.OrderBy(t => t.Nip).ToListAsync(ct);
        var subjectIds = await db.Subjects.ToDictionaryAsync(s => s.Name, s => s.Id, ct);
        if (classrooms.Count == 0 || teachers.Count < Subjects.Length || subjectIds.Count < Subjects.Length)
        {
            return;
        }

        (TimeOnly Start, TimeOnly End)[] slots =
        [
            (new TimeOnly(7, 0), new TimeOnly(8, 30)),
            (new TimeOnly(8, 30), new TimeOnly(10, 0)),
            (new TimeOnly(10, 15), new TimeOnly(11, 45)),
            (new TimeOnly(12, 30), new TimeOnly(14, 0)),
        ];
        DayOfWeek[] days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

        for (var d = 0; d < days.Length; d++)
        {
            for (var s = 0; s < slots.Length; s++)
            {
                for (var c = 0; c < classrooms.Count; c++)
                {
                    var subject = (s + c + d) % Subjects.Length;
                    db.Schedules.Add(Schedule.Create(
                        classrooms[c].Id,
                        subjectIds[Subjects[subject].Name],
                        teachers[subject].Id,
                        days[d],
                        slots[s].Start,
                        slots[s].End));
                }
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded the timetable of {Classrooms} classrooms", classrooms.Count);
    }

    private async Task SeedCommunicationAsync(CancellationToken ct)
    {
        if (await db.NewsArticles.IgnoreQueryFilters().AnyAsync(ct) || await db.Announcements.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == Email("admin"), ct);
        if (admin is null)
        {
            return; // no school data to attach the announcements to
        }

        var now = timeProvider.GetUtcNow();
        AddNews("Welcome to the 2026/2027 academic year", NewsCategory.News, now.AddDays(-14),
            "<p>A warm welcome to all students, parents, and teachers. Classes start on <strong>13 July</strong>.</p>"
            + "<p>Please check the announcements page for your class schedule and the first-week program.</p>");
        AddNews("Open house for new applicants", NewsCategory.AdmissionInfo, now.AddDays(-7),
            "<p>Prospective students and their parents are invited to our open house on the first Saturday of the month.</p>"
            + "<ul><li>Tour of the classrooms and laboratories</li><li>Meet the teachers</li><li>How to apply online</li></ul>");
        AddNews("Our students won the regional science olympiad", NewsCategory.Award, now.AddDays(-3),
            "<p>Congratulations to the science team for winning first place at the regional olympiad.</p>");
        AddNews("Library renovation schedule", NewsCategory.News, null,
            "<p>The library will be renovated during the mid-term break. The schedule is still being prepared.</p>");

        db.Announcements.Add(PublishedAnnouncement(
            "School closed on Friday for a teachers' meeting", isPublic: true, [], now.AddDays(-2), admin.Id,
            "<p>All classes are suspended this Friday. Classes resume on Monday as usual.</p>"));
        db.Announcements.Add(PublishedAnnouncement(
            "Report card meeting next week", isPublic: false, [UserRole.Teacher], now.AddDays(-1), admin.Id,
            "<p>All teachers are asked to attend the report card meeting on Wednesday after the last lesson.</p>"));
        db.Announcements.Add(Announcement.Create(
            "Parent-teacher conference", "<p>Dates and time slots will follow.</p>", isPublic: false, [UserRole.Parent], [], admin.Id));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded demo news and announcements");
    }

    private void AddNews(string title, NewsCategory category, DateTimeOffset? publishedAt, string body)
    {
        var article = NewsArticle.Create(title, SlugGenerator.Slugify(title), body, category, metaDescription: null);
        if (publishedAt is { } at)
        {
            article.Publish(at);
        }

        db.NewsArticles.Add(article);
    }

    private static Announcement PublishedAnnouncement(
        string title, bool isPublic, IEnumerable<UserRole> roles, DateTimeOffset publishedAt, Guid createdById, string body)
    {
        var announcement = Announcement.Create(title, body, isPublic, roles, [], createdById);
        announcement.Publish(publishedAt);
        return announcement;
    }

    private List<Teacher> SeedTeachers()
    {
        var teachers = new List<Teacher>();
        for (var n = 1; n <= TeacherCount; n++)
        {
            var email = n == 1 ? Email("teacher") : Email($"teacher{n:00}");
            var name = n == 1 ? "Budi Santoso" : FullName(n + 3);
            var user = User.Create(email, name, UserRole.Teacher);
            var teacher = Teacher.Create(user.Id, string.Create(CultureInfo.InvariantCulture, $"198001012005011{n:000}"));
            db.Users.Add(user);
            db.Teachers.Add(teacher);
            teachers.Add(teacher);
        }

        return teachers;
    }

    private List<Student> SeedStudents(List<Classroom> classrooms)
    {
        var students = new List<Student>();
        var n = 0;
        foreach (var classroom in classrooms)
        {
            for (var i = 0; i < StudentsPerClassroom; i++)
            {
                n++;
                var email = n == 1 ? Email("student") : Email($"student{n:000}");
                var name = n == 1 ? "Rina Putri" : FullName(n * 7);
                var user = User.Create(email, name, UserRole.Student);
                var dateOfBirth = new DateOnly(2012 + (n % 3), 1 + (n % 12), 1 + (n % 28));
                var student = Student.Create(user.Id, string.Create(CultureInfo.InvariantCulture, $"{1000000000L + n}"), classroom.Id, dateOfBirth);
                db.Users.Add(user);
                db.Students.Add(student);
                students.Add(student);
            }
        }

        return students;
    }

    private User AddUser(string localPart, string fullName, UserRole role)
    {
        var user = User.Create(Email(localPart), fullName, role);
        db.Users.Add(user);
        return user;
    }

    private static string Email(string localPart) => $"{localPart}@{DemoDomain}";

    private static string FullName(int seed) =>
        $"{FirstNames[seed % FirstNames.Length]} {LastNames[(seed * 3) % LastNames.Length]}";
}
