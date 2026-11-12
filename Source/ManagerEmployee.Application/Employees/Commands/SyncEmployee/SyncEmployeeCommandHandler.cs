using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Events;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Repositories;

namespace ManagerEmployee.Application.Employees.Commands.SyncEmployee;

public sealed class SyncEmployeeCommandHandler(
    IEnumerable<IProviderEmployeeMapper> mappers,
    IEmployeeRepository employeeRepository,
    ITrackTikEmployeeClient trackTikEmployeeClient,
    IEmployeeSyncedEventPublisher eventPublisher,
    EmployeeSynchronizationCoordinator synchronizationCoordinator,
    TimeProvider timeProvider)
{
    public async Task<SyncEmployeeResult> HandleAsync(
        SyncEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var mapper = EmployeeProviderExtensions.TryParseContractValue(command.Provider, out var provider)
            ? mappers.FirstOrDefault(candidate => candidate.Provider == provider)
            : null;
        if (mapper is null)
        {
            return SyncEmployeeResult.UnknownProvider();
        }

        var mappingResult = mapper.Map(command.Data);
        if (!mappingResult.IsValid || mappingResult.Employee is null)
        {
            return SyncEmployeeResult.ValidationFailed(mappingResult.Errors);
        }

        var mapped = mappingResult.Employee;
        await using var coordination = await synchronizationCoordinator.AcquireAsync(
            mapped.Provider,
            mapped.ProviderEmployeeId,
            cancellationToken);

        var employee = await employeeRepository.FindByIdentityAsync(
            mapped.Provider,
            mapped.ProviderEmployeeId,
            cancellationToken);
        var shouldUpdateTrackTik = employee?.HasBeenCreatedInTrackTik == true;
        var attempt = CreateAttempt(mapped, timeProvider.GetUtcNow());

        if (employee is null)
        {
            employee = Employee.CreateForSynchronization(
                EmployeeIdentity.Create(mapped.Provider, mapped.ProviderEmployeeId),
                attempt);

            await employeeRepository.AddAsync(employee, cancellationToken);
        }
        else
        {
            employee.PrepareResubmission(attempt);
        }

        if (await employeeRepository.CommitAsync(cancellationToken) == EmployeeCommitOutcome.Conflict)
        {
            return SyncEmployeeResult.PersistenceConflict();
        }

        var synchronizationResult = await SynchronizeAsync(
            TrackTikEmployee.FromDomain(employee),
            shouldUpdateTrackTik,
            cancellationToken);

        if (!synchronizationResult.IsSuccess)
        {
            employee.MarkFailed(SynchronizationError.Create(
                synchronizationResult.Error ?? "TrackTik synchronization failed."));
            if (await employeeRepository.CommitAsync(cancellationToken) == EmployeeCommitOutcome.Conflict)
            {
                return SyncEmployeeResult.PersistenceConflict();
            }

            return SyncEmployeeResult.SynchronizationFailed(EmployeeDetails.FromDomain(employee));
        }

        var employeeSynced = employee.MarkSynced(timeProvider.GetUtcNow());
        if (await employeeRepository.CommitAsync(cancellationToken) == EmployeeCommitOutcome.Conflict)
        {
            return SyncEmployeeResult.PersistenceConflict();
        }

        await eventPublisher.PublishAsync(employeeSynced, cancellationToken);
        return SyncEmployeeResult.Synchronized(EmployeeDetails.FromDomain(employee));
    }

    private async Task<TrackTikSyncResult> SynchronizeAsync(
        TrackTikEmployee employee,
        bool shouldUpdate,
        CancellationToken cancellationToken)
    {
        try
        {
            return shouldUpdate
                ? await trackTikEmployeeClient.UpdateAsync(employee, cancellationToken)
                : await trackTikEmployeeClient.CreateAsync(employee, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return TrackTikSyncResult.Failure(
                "TrackTik request failed before receiving a successful response.");
        }
    }

    private static EmployeeSynchronizationAttempt CreateAttempt(
        MappedEmployee mapped,
        DateTimeOffset attemptedAt) => EmployeeSynchronizationAttempt.Create(
            EmployeeProfile.Create(
                mapped.FirstName,
                mapped.LastName,
                mapped.Email,
                mapped.PhoneNumber,
                mapped.Position,
                mapped.Department,
                mapped.StartDate,
                mapped.EmploymentStatus),
            attemptedAt);
}
