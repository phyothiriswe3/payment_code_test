using System.Text.Json;
using Checkout.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Checkout.Infrastructure.Redis;

public sealed class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(key);
            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>(cached.ToString(), Json);
                if (value is not null) return value;
            }
        }
        catch (Exception ex) when (ex is RedisException or JsonException or TimeoutException)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}; falling back to source", key);
        }

        var result = await factory(ct);

        try
        {
            await redis.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(result, Json), ttl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }

        return result;
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Cache remove failed for {Key}", key);
        }
    }
}
