using System.Text.Json;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;
using ManagerEmployee.Infrastructure.Providers.Provider1;

namespace ManagerEmployee.Infrastructure.Tests.Providers;

[TestClass]
public sealed class Provider1EmployeeMapperTests
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);
    private readonly Provider1EmployeeMapper _mapper = new();

    [TestMethod]
    [DataRow(
        "P1_001",
        "Alice",
        "Johnson",
        "alice.johnson@provider1.com",
        "+1-555-0101",
        "Security Officer",
        "Security Operations",
        "2024-01-15")]
    [DataRow(
        "P1_002",
        "Bob",
        "Smith",
        "bob.smith@provider1.com",
        "+1-555-0102",
        "Supervisor",
        "Security Operations",
        "2023-11-20")]
    public void Map_WithSampleEmployee_ProducesExpectedTrackTikEmployee(
        string employeeId,
        string firstName,
        string lastName,
        string email,
        string phone,
        string position,
        string department,
        string startDate)
    {
        var json = $$"""
            {
              "emp_id": "{{employeeId}}",
              "first_name": "{{firstName}}",
              "last_name": "{{lastName}}",
              "email_address": "{{email}}",
              "phone": "{{phone}}",
              "job_title": "{{position}}",
              "dept": "{{department}}",
              "hire_date": "{{startDate}}",
              "employment_status": "active"
            }
            """;

        var mapped = MapValid(json);
        var trackTikEmployee = ToTrackTikEmployee(mapped);

        Assert.AreEqual($"provider1-{employeeId}", trackTikEmployee.EmployeeId);
        Assert.AreEqual(firstName, trackTikEmployee.FirstName);
        Assert.AreEqual(lastName, trackTikEmployee.LastName);
        Assert.AreEqual(email, trackTikEmployee.Email);
        Assert.AreEqual(phone, trackTikEmployee.PhoneNumber);
        Assert.AreEqual(position, trackTikEmployee.Position);
        Assert.AreEqual(department, trackTikEmployee.Department);
        Assert.AreEqual(DateOnly.Parse(startDate), trackTikEmployee.StartDate);
        Assert.AreEqual(EmployeeStatus.Active, trackTikEmployee.Status);
    }

    [TestMethod]
    [DataRow("active", EmployeeStatus.Active)]
    [DataRow("inactive", EmployeeStatus.Inactive)]
    [DataRow("terminated", EmployeeStatus.Terminated)]
    public void Map_WithSupportedStatus_MapsStatusDirectly(string sourceStatus, EmployeeStatus expected)
    {
        var mapped = MapValid(RequiredJson(sourceStatus));

        Assert.AreEqual(expected, mapped.EmploymentStatus);
    }

    [TestMethod]
    public void Map_WithMissingRequiredTrackTikStatus_ReturnsValidationError()
    {
        using var document = JsonDocument.Parse(RequiredJson(null));

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "employment_status"));
    }

    [TestMethod]
    public void Map_WithInvalidEmailAndDate_ReturnsBothValidationErrors()
    {
        const string json = """
            {
              "emp_id": "P1_001",
              "first_name": "Alice",
              "last_name": "Johnson",
              "email_address": "not-an-email",
              "hire_date": "01/15/2024",
              "employment_status": "active"
            }
            """;
        using var document = JsonDocument.Parse(json);

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "email_address"));
        Assert.IsTrue(result.Errors.Any(error => error.Field == "hire_date"));
    }

    [TestMethod]
    public void Map_WithProviderEmployeeIdBeyondDomainLimit_ReturnsValidationError()
    {
        var employeeId = new string('x', EmployeeFieldLimits.ProviderEmployeeId + 1);
        using var document = JsonDocument.Parse(RequiredJson("active").Replace("P1_001", employeeId));

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "emp_id"));
    }

    private static string RequiredJson(string? status)
    {
        var statusProperty = status is null ? string.Empty : $",\"employment_status\":\"{status}\"";
        return $$"""
            {
              "emp_id": "P1_001",
              "first_name": "Alice",
              "last_name": "Johnson",
              "email_address": "alice.johnson@provider1.com"{{statusProperty}}
            }
            """;
    }

    private MappedEmployee MapValid(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = _mapper.Map(document.RootElement);
        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.IsNotNull(result.Employee);
        return result.Employee;
    }

    private static TrackTikEmployee ToTrackTikEmployee(MappedEmployee mapped)
    {
        var employee = Employee.CreateForSynchronization(
            EmployeeIdentity.Create(mapped.Provider, mapped.ProviderEmployeeId),
            EmployeeSynchronizationAttempt.Create(
                EmployeeProfile.Create(
                    mapped.FirstName,
                    mapped.LastName,
                    mapped.Email,
                    mapped.PhoneNumber,
                    mapped.Position,
                    mapped.Department,
                    mapped.StartDate,
                    mapped.EmploymentStatus),
                AttemptedAt));
        return TrackTikEmployee.FromDomain(employee);
    }
}
