using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Policies;
using ManagerEmployee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class ManagerEmployeeDbContextTests
{
    [TestMethod]
    public void Model_EnforcesEmployeeIdentityAndPersistenceTypes()
    {
        var options = new DbContextOptionsBuilder<ManagerEmployeeDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelInspection;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new ManagerEmployeeDbContext(options);
        var employeeType = context.Model.FindEntityType(typeof(Employee));

        Assert.IsNotNull(employeeType);

        CollectionAssert.AreEqual(
            new[] { nameof(Employee.Provider), nameof(Employee.ProviderEmployeeId) },
            employeeType.FindPrimaryKey()?.Properties.Select(property => property.Name).ToArray());
        Assert.IsNull(employeeType.FindProperty("Id"));
        Assert.AreEqual("date", employeeType.FindProperty(nameof(Employee.StartDate))?.GetColumnType());
        Assert.IsTrue(employeeType.FindProperty("RowVersion")?.IsConcurrencyToken);
        Assert.AreEqual(
            EmployeeFieldLimits.Email,
            employeeType.FindProperty(nameof(Employee.Email))?.GetMaxLength());
        Assert.IsNotNull(employeeType.FindProperty(nameof(Employee.HasBeenCreatedInTrackTik)));
    }
}
