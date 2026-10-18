using Checkout.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Checkout.Infrastructure.Redis;

public sealed class RedisDistributedLock(IConnectionMultiplexer redis, ILogger<RedisDistributedLock> logger) : IDistributedLock
{
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    // Redis exceptions propagate so callers can decide on a fallback.
    public async Task<ILockHandle?> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = "lock:" + resource;
        var owner = Guid.NewGuid().ToString("N");
        var acquired = await redis.GetDatabase().StringSetAsync(key, owner, ttl, When.NotExists);
        return acquired ? new Handle(redis, logger, resource, key, owner) : null;
    }

    private sealed class Handle(IConnectionMultiplexer redis, ILogger logger, string resource, string key, string owner) : ILockHandle
    {
        private int _released;
        public string Resource => resource;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1) return;
            try
            {
                await redis.GetDatabase().ScriptEvaluateAsync(ReleaseScript, [(RedisKey)key], [(RedisValue)owner]);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to release lock {Resource}; it will expire via TTL", resource);
            }
        }
    }
}
