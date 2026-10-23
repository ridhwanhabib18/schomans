using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
