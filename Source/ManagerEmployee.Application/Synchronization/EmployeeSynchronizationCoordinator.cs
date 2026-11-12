using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Synchronization;

public sealed class EmployeeSynchronizationCoordinator : IDisposable
{
    private const int StripeCount = 64;
    private readonly SemaphoreSlim[] _stripes = Enumerable.Range(0, StripeCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        EmployeeProvider provider,
        string providerEmployeeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEmployeeId);

        var hash = HashCode.Combine(provider, providerEmployeeId);
        var stripe = _stripes[(uint)hash % StripeCount];
        await stripe.WaitAsync(cancellationToken);
        return new Lease(stripe);
    }

    public void Dispose()
    {
        foreach (var stripe in _stripes)
        {
            stripe.Dispose();
        }
    }

    private sealed class Lease(SemaphoreSlim stripe) : IAsyncDisposable
    {
        private bool _isReleased;

        public ValueTask DisposeAsync()
        {
            if (!_isReleased)
            {
                stripe.Release();
                _isReleased = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
