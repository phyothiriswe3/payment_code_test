namespace Checkout.Application.Abstractions;

public interface IDistributedLock
{
    /// <summary>Returns null when another owner holds the lock.</summary>
    Task<ILockHandle?> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken ct);
}

/// <summary>Disposing releases the lock only if still owned (compare-and-delete).</summary>
public interface ILockHandle : IAsyncDisposable
{
    string Resource { get; }
}
