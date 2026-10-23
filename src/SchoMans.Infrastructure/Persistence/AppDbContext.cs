using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Common;
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
