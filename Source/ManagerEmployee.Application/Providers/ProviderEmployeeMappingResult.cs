namespace ManagerEmployee.Application.Providers;

public sealed class ProviderEmployeeMappingResult
{
    private ProviderEmployeeMappingResult(
        MappedEmployee? employee,
        IReadOnlyList<ProviderValidationError> errors)
    {
        Employee = employee;
        Errors = errors;
    }

    public bool IsValid => Employee is not null && Errors.Count == 0;

    public MappedEmployee? Employee { get; }

    public IReadOnlyList<ProviderValidationError> Errors { get; }

    public static ProviderEmployeeMappingResult Success(MappedEmployee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        return new ProviderEmployeeMappingResult(employee, []);
    }

    public static ProviderEmployeeMappingResult Failure(IEnumerable<ProviderValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorList = errors.ToArray();
        if (errorList.Length == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(errors));
        }

        return new ProviderEmployeeMappingResult(null, errorList);
    }
}
