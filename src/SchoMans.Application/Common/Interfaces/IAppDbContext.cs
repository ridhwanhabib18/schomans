using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Common;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>The only gateway to the database. There is no generic repository on top of it.</summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Student> Students { get; }

    DbSet<Teacher> Teachers { get; }

    DbSet<StudentParent> StudentParents { get; }

    DbSet<AcademicYear> AcademicYears { get; }

    DbSet<Term> Terms { get; }

    DbSet<Classroom> Classrooms { get; }

    DbSet<Subject> Subjects { get; }

    DbSet<Schedule> Schedules { get; }

    DbSet<AttendanceSession> AttendanceSessions { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<Assignment> Assignments { get; }

    DbSet<Submission> Submissions { get; }

    DbSet<GradeWeight> GradeWeights { get; }

    DbSet<Grade> Grades { get; }

    DbSet<GradePublication> GradePublications { get; }

    DbSet<ReportCard> ReportCards { get; }

    DbSet<AchievementDescription> AchievementDescriptions { get; }

    DbSet<GradeBand> GradeBands { get; }

    DbSet<AdmissionPeriod> AdmissionPeriods { get; }

    DbSet<Applicant> Applicants { get; }

    DbSet<ApplicantDocument> ApplicantDocuments { get; }

    DbSet<ApprovalChainStep> ApprovalChainSteps { get; }

    DbSet<Request> Requests { get; }

    DbSet<Approval> Approvals { get; }

    DbSet<Letter> Letters { get; }

    DbSet<NewsArticle> NewsArticles { get; }

    DbSet<Announcement> Announcements { get; }

    DbSet<AnnouncementRead> AnnouncementReads { get; }

    DbSet<Notification> Notifications { get; }

    ChangeTracker ChangeTracker { get; }

    /// <summary>Needed for explicit transactions (through an execution strategy, because SQL Server retries are enabled).</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the next save fail with a concurrency exception if somebody changed the row since the client read it
    /// (the client sends back the row version it got with the data), and makes the save bump the version of the row.
    /// </summary>
    void SetExpectedRowVersion(BaseEntity entity, byte[] rowVersion);
}
