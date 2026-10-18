namespace Checkout.Application.Abstractions;

public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
