using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ManagerEmployee.Infrastructure.Persistence;

public sealed class ManagerEmployeeDbContextFactory : IDesignTimeDbContextFactory<ManagerEmployeeDbContext>
{
    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=ManagerEmployee;Integrated Security=True;TrustServerCertificate=True";

    public ManagerEmployeeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__ManagerEmployeeDatabase");

        var options = new DbContextOptionsBuilder<ManagerEmployeeDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString)
                ? DefaultConnectionString
                : connectionString)
            .Options;

        return new ManagerEmployeeDbContext(options);
    }
}
