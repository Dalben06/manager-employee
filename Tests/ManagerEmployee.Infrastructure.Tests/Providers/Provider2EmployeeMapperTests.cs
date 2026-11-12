using System.Text.Json;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;
using ManagerEmployee.Infrastructure.Providers.Provider2;

namespace ManagerEmployee.Infrastructure.Tests.Providers;

[TestClass]
public sealed class Provider2EmployeeMapperTests
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);
    private readonly Provider2EmployeeMapper _mapper = new();

    [TestMethod]
    [DataRow(
        "P2_001",
        "Carol",
        "Davis",
        "carol.davis@provider2.com",
        "+1-555-0201",
        "Security Guard",
        "Night Shift Security",
        "2024-02-01")]
    [DataRow(
        "P2_002",
        "David",
        "Wilson",
        "david.wilson@provider2.com",
        "+1-555-0202",
        "Team Lead",
        "Day Shift Security",
        "2023-12-15")]
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
        var json = CreateJson(
            employeeId,
            firstName,
            lastName,
            email,
            phone,
            position,
            department,
            startDate,
            "employed");

        var mapped = MapValid(json);
        var trackTikEmployee = ToTrackTikEmployee(mapped);

        Assert.AreEqual($"provider2-{employeeId}", trackTikEmployee.EmployeeId);
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
    [DataRow("employed", EmployeeStatus.Active)]
    [DataRow("on_leave", EmployeeStatus.Inactive)]
    [DataRow("terminated", EmployeeStatus.Terminated)]
    public void Map_WithProviderStatus_ConvertsStatus(string sourceStatus, EmployeeStatus expected)
    {
        var mapped = MapValid(CreateJson(status: sourceStatus));

        Assert.AreEqual(expected, mapped.EmploymentStatus);
    }

    [TestMethod]
    public void Map_WithMissingNestedObjects_ReturnsValidationErrors()
    {
        const string json = """{"employee_number":"P2_001"}""";
        using var document = JsonDocument.Parse(json);

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "personal_info"));
        Assert.IsTrue(result.Errors.Any(error => error.Field == "work_info"));
    }

    [TestMethod]
    public void Map_WithMissingNestedTrackTikFields_ReturnsValidationErrors()
    {
        const string json = """
            {
              "employee_number": "P2_001",
              "personal_info": {},
              "work_info": {}
            }
            """;
        using var document = JsonDocument.Parse(json);

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "personal_info.given_name"));
        Assert.IsTrue(result.Errors.Any(error => error.Field == "personal_info.family_name"));
        Assert.IsTrue(result.Errors.Any(error => error.Field == "personal_info.email"));
        Assert.IsTrue(result.Errors.Any(error => error.Field == "work_info.current_status"));
    }

    [TestMethod]
    public void Map_WithInvalidDate_ReturnsValidationError()
    {
        var json = CreateJson(startDate: "2024/02/01");
        using var document = JsonDocument.Parse(json);

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "work_info.start_date"));
    }

    [TestMethod]
    public void Map_WithPhoneBeyondDomainLimit_ReturnsValidationError()
    {
        var json = CreateJson(phone: new string('1', EmployeeFieldLimits.PhoneNumber + 1));
        using var document = JsonDocument.Parse(json);

        var result = _mapper.Map(document.RootElement);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Field == "personal_info.mobile"));
    }

    private static string CreateJson(
        string employeeId = "P2_001",
        string firstName = "Carol",
        string lastName = "Davis",
        string email = "carol.davis@provider2.com",
        string phone = "+1-555-0201",
        string position = "Security Guard",
        string department = "Night Shift Security",
        string startDate = "2024-02-01",
        string status = "employed") => $$"""
            {
              "employee_number": "{{employeeId}}",
              "personal_info": {
                "given_name": "{{firstName}}",
                "family_name": "{{lastName}}",
                "email": "{{email}}",
                "mobile": "{{phone}}"
              },
              "work_info": {
                "role": "{{position}}",
                "division": "{{department}}",
                "start_date": "{{startDate}}",
                "current_status": "{{status}}"
              }
            }
            """;

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
