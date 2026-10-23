using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Seeder;

/// <summary>
/// Fills an empty database with one fictional school: one academic year, 5 classrooms, 15 teachers and 150 students,
/// plus one login-ready account per role. Every name and number is made up; nothing here is real personal data.
/// The demo accounts have no Keycloak id yet; the first login links them by email (see SyncUserHandler).
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, ILogger<DemoDataSeeder> logger)
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

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("Demo data not seeded: the database already contains users");
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
            "Seeded demo data: {Teachers} teachers, {Classrooms} classrooms, {Students} students",
            teachers.Count,
            classrooms.Count,
            students.Count);
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
