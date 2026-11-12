using System.Net.Mail;

namespace ManagerEmployee.Domain.Employees.Policies;

public static class EmployeeEmailPolicy
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return MailAddress.TryCreate(normalized, out var parsed) &&
            string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase);
    }
}
