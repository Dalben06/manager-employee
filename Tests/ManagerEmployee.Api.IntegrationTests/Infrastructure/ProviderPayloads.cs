namespace ManagerEmployee.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Provider payloads taken from the challenge sample data.
/// </summary>
internal static class ProviderPayloads
{
    public const string Provider1Alice = """
        {
          "emp_id": "P1_001",
          "first_name": "Alice",
          "last_name": "Johnson",
          "email_address": "alice.johnson@provider1.com",
          "phone": "+1-555-0101",
          "job_title": "Security Officer",
          "dept": "Security Operations",
          "hire_date": "2024-01-15",
          "employment_status": "active"
        }
        """;

    public const string Provider1InvalidEmail = """
        {
          "emp_id": "P1_999",
          "first_name": "Invalid",
          "last_name": "Email",
          "email_address": "not-an-email",
          "employment_status": "active"
        }
        """;

    public const string Provider2Carol = """
        {
          "employee_number": "P2_001",
          "personal_info": {
            "given_name": "Carol",
            "family_name": "Davis",
            "email": "carol.davis@provider2.com",
            "mobile": "+1-555-0201"
          },
          "work_info": {
            "role": "Security Guard",
            "division": "Night Shift Security",
            "start_date": "2024-02-01",
            "current_status": "employed"
          }
        }
        """;
}
