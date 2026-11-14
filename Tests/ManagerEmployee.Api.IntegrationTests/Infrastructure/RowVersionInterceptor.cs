using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ManagerEmployee.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The EF Core in-memory provider does not generate SQL Server <c>rowversion</c> values.
/// This interceptor stamps a new value on every insert or update, as SQL Server would.
/// </summary>
internal sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    private const string RowVersion = "RowVersion";
    private long _version;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context?.ChangeTracker.Entries()
            .Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified &&
                entry.Metadata.FindProperty(RowVersion) is not null) ?? [];

        foreach (var entry in entries)
        {
            entry.Property(RowVersion).CurrentValue = BitConverter.GetBytes(Interlocked.Increment(ref _version));
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
