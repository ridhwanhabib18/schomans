using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Common;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<StudentParent> StudentParents => Set<StudentParent>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<Term> Terms => Set<Term>();

    public DbSet<Classroom> Classrooms => Set<Classroom>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<GradeWeight> GradeWeights => Set<GradeWeight>();

    public DbSet<Grade> Grades => Set<Grade>();

    public DbSet<GradePublication> GradePublications => Set<GradePublication>();

    public DbSet<ReportCard> ReportCards => Set<ReportCard>();

    public DbSet<AchievementDescription> AchievementDescriptions => Set<AchievementDescription>();

    public DbSet<GradeBand> GradeBands => Set<GradeBand>();

    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public void SetExpectedRowVersion(BaseEntity entity, byte[] rowVersion)
    {
        var entry = Entry(entity);
        entry.Property(e => e.RowVersion).OriginalValue = rowVersion;

        // An edit always bumps the version, even when only a related row changed (the account behind a student, the
        // terms of a year). Without this the next editor with a stale version would not be noticed.
        entry.State = EntityState.Modified;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Optimistic concurrency and soft delete apply to every entity.
            modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.RowVersion)).IsRowVersion();
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(NotDeletedFilter(entityType.ClrType));
        }
    }

    private static LambdaExpression NotDeletedFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var isDeleted = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        return Expression.Lambda(Expression.Equal(isDeleted, Expression.Constant(false)), parameter);
    }
}
