using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ManagerEmployee.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "Employees",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_Employees_Provider",
                    "[Provider] IN ('provider1', 'provider2')");
                tableBuilder.HasCheckConstraint(
                    "CK_Employees_EmploymentStatus",
                    "[EmploymentStatus] IN ('active', 'inactive', 'terminated')");
                tableBuilder.HasCheckConstraint(
                    "CK_Employees_SyncStatus",
                    "[SyncStatus] IN ('pending', 'synced', 'failed')");
                tableBuilder.HasCheckConstraint(
                    "CK_Employees_Attempts",
                    "[Attempts] >= 1");
            });

        builder.HasKey(employee => new { employee.Provider, employee.ProviderEmployeeId });

        builder.Property(employee => employee.Provider)
            .HasConversion(new ValueConverter<EmployeeProvider, string>(
                provider => provider.ToContractValue(),
                value => FromProviderValue(value)))
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(employee => employee.ProviderEmployeeId)
            .HasMaxLength(EmployeeFieldLimits.ProviderEmployeeId)
            .IsRequired();

        builder.Property(employee => employee.FirstName)
            .HasMaxLength(EmployeeFieldLimits.Name)
            .IsRequired();

        builder.Property(employee => employee.LastName)
            .HasMaxLength(EmployeeFieldLimits.Name)
            .IsRequired();

        builder.Property(employee => employee.Email)
            .HasMaxLength(EmployeeFieldLimits.Email)
            .IsRequired();

        builder.Property(employee => employee.PhoneNumber)
            .HasMaxLength(EmployeeFieldLimits.PhoneNumber);

        builder.Property(employee => employee.Position)
            .HasMaxLength(EmployeeFieldLimits.Position);

        builder.Property(employee => employee.Department)
            .HasMaxLength(EmployeeFieldLimits.Department);

        builder.Property(employee => employee.StartDate)
            .HasColumnType("date");

        builder.Property(employee => employee.EmploymentStatus)
            .HasConversion(new ValueConverter<EmployeeStatus, string>(
                status => status.ToContractValue(),
                value => FromEmployeeStatusValue(value)))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(employee => employee.SyncStatus)
            .HasConversion(new ValueConverter<SynchronizationStatus, string>(
                status => status.ToContractValue(),
                value => FromSynchronizationStatusValue(value)))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(employee => employee.Attempts).IsRequired();
        builder.Property(employee => employee.LastAttemptAt);

        builder.Property(employee => employee.LastError)
            .HasMaxLength(EmployeeFieldLimits.LastError);

        builder.Property(employee => employee.HasBeenCreatedInTrackTik)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
    }

    private static EmployeeProvider FromProviderValue(string value) => value switch
    {
        "provider1" => EmployeeProvider.Provider1,
        "provider2" => EmployeeProvider.Provider2,
        _ => throw new InvalidOperationException($"Unsupported provider value '{value}'."),
    };

    private static EmployeeStatus FromEmployeeStatusValue(string value) => value switch
    {
        "active" => EmployeeStatus.Active,
        "inactive" => EmployeeStatus.Inactive,
        "terminated" => EmployeeStatus.Terminated,
        _ => throw new InvalidOperationException($"Unsupported employment status value '{value}'."),
    };

    private static SynchronizationStatus FromSynchronizationStatusValue(string value) => value switch
    {
        "pending" => SynchronizationStatus.Pending,
        "synced" => SynchronizationStatus.Synced,
        "failed" => SynchronizationStatus.Failed,
        _ => throw new InvalidOperationException($"Unsupported synchronization status value '{value}'."),
    };
}
