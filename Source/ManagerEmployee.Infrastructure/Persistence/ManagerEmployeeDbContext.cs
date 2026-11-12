using ManagerEmployee.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Persistence;

public sealed class ManagerEmployeeDbContext(DbContextOptions<ManagerEmployeeDbContext> options)
    : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ManagerEmployeeDbContext).Assembly);
    }
}
