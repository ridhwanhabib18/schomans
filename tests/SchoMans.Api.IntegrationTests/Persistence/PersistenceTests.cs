using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;
using SchoMans.Infrastructure.Persistence.Seeder;

namespace SchoMans.Api.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public class PersistenceTests(ApiFixture fixture)
{
    private AsyncServiceScope NewScope() => fixture.Factory.Services.CreateAsyncScope();

    private static string UniqueEmail() => $"p-{Guid.NewGuid():N}@test.example";

    [Fact]
    public async Task Migrations_and_seed_produce_the_demo_school()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The database is shared with other tests, so count the seeded demo school only.
        Assert.Equal(15, await db.Teachers.CountAsync(t => t.User!.Email.EndsWith("@schomans.demo")));
        Assert.Equal(5, await db.Classrooms.CountAsync(c => c.AcademicYear!.Name == "2026/2027" && c.Name.StartsWith("Class ") && c.Name.Length == 7));
        Assert.Equal(150, await db.Students.CountAsync(s => s.User!.Email.EndsWith("@schomans.demo")));
        Assert.Equal(2, await db.Terms.CountAsync(t => t.AcademicYear!.Name == "2026/2027"));
        string[] subjects = ["Mathematics", "Indonesian Language", "English", "Natural Sciences", "Social Studies", "Arts and Culture", "Physical Education", "Civics"];
        Assert.Equal(subjects.Length, await db.Subjects.CountAsync(s => subjects.Contains(s.Name)));
        Assert.Single(await db.AcademicYears.Where(a => a.IsActive).ToListAsync());

        var demoRoles = await db.Users
            .Where(u => u.Email.EndsWith("@schomans.demo") && !u.Email.Contains("teacher") && !u.Email.Contains("student"))
            .Select(u => u.Role)
            .ToListAsync();
        Assert.Contains(UserRole.Principal, demoRoles);
        Assert.Contains(UserRole.Admin, demoRoles);
        Assert.Contains(UserRole.Parent, demoRoles);
        Assert.Contains(UserRole.Applicant, demoRoles);
    }

    [Fact]
    public async Task Demo_parent_has_two_children_and_demo_teacher_is_homeroom_of_a_classroom()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Other tests link the demo parent to extra students of their own, so count the seeded children only.
        var children = await db.StudentParents.CountAsync(p => p.ParentUser!.Email == "parent@schomans.demo" && p.Student!.User!.Email.EndsWith("@schomans.demo"));
        var homeroomClassrooms = await db.Classrooms.CountAsync(c => c.HomeroomTeacher!.User!.Email == "teacher@schomans.demo");

        Assert.Equal(2, children);
        Assert.Equal(1, homeroomClassrooms);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_anything()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.CountAsync();
        var students = await db.Students.CountAsync();
        var subjects = await db.Subjects.CountAsync();
        var news = await db.NewsArticles.CountAsync();
        var announcements = await db.Announcements.CountAsync();

        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();

        Assert.Equal(users, await db.Users.CountAsync());
        Assert.Equal(students, await db.Students.CountAsync());
        Assert.Equal(subjects, await db.Subjects.CountAsync());
        Assert.Equal(news, await db.NewsArticles.CountAsync());
        Assert.Equal(announcements, await db.Announcements.CountAsync());
    }

    [Fact]
    public async Task Timestamps_are_set_on_insert_and_refreshed_on_update()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create(UniqueEmail(), "Time Keeper", UserRole.Student);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        var created = user.CreatedAt;
        Assert.NotEqual(default, created);
        Assert.Equal(created, user.UpdatedAt);

        await Task.Delay(20);
        user.SyncProfile(user.Email, "Time Keeper Renamed", UserRole.Student);
        await db.SaveChangesAsync();

        Assert.Equal(created, user.CreatedAt);
        Assert.True(user.UpdatedAt > created);
    }

    [Fact]
    public async Task Removing_an_entity_soft_deletes_it()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create(UniqueEmail(), "Soon Gone", UserRole.Student);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        Assert.False(await db.Users.AnyAsync(u => u.Id == user.Id));
        var row = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task Email_is_unique_among_live_users_but_free_again_after_a_soft_delete()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = UniqueEmail();
        var first = User.Create(email, "First Owner", UserRole.Student);
        db.Users.Add(first);
        await db.SaveChangesAsync();

        db.Users.Add(User.Create(email.ToUpperInvariant(), "Duplicate", UserRole.Student));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.Users.Remove((await db.Users.FindAsync(first.Id))!);
        await db.SaveChangesAsync();
        db.Users.Add(User.Create(email, "Second Owner", UserRole.Student));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Concurrent_updates_of_the_same_row_are_detected()
    {
        await using var scopeA = NewScope();
        await using var scopeB = NewScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create(UniqueEmail(), "Contested", UserRole.Student);
        dbA.Users.Add(user);
        await dbA.SaveChangesAsync();

        var copyA = await dbA.Users.SingleAsync(u => u.Id == user.Id);
        var copyB = await dbB.Users.SingleAsync(u => u.Id == user.Id);
        copyA.SyncProfile(copyA.Email, "Edited By A", UserRole.Student);
        copyB.SyncProfile(copyB.Email, "Edited By B", UserRole.Student);
        await dbA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task Only_one_academic_year_can_be_active()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AcademicYears.Add(AcademicYear.Create("2099/2100", new DateOnly(2099, 7, 1), new DateOnly(2100, 6, 30), isActive: true));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
