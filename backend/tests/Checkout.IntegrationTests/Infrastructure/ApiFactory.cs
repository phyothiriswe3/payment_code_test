using Checkout.Application.Abstractions;
using Checkout.Infrastructure.Payments.TwoC2P;
using Checkout.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Testcontainers.MySql;
using Testcontainers.Redis;

namespace Checkout.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}

/// <summary>
/// Real API against throwaway MySQL 8.0 + Redis 7 containers (started once per collection).
/// 2C2P is replaced by the singleton <see cref="Payments"/>. Call <see cref="ResetAsync"/> at the start of each test.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Test-only HS256 key (not the sandbox secret).</summary>
    public const string TestSecret = "integration-test-secret-key-0123456789abcdef";

    private readonly MySqlContainer _mySql = new MySqlBuilder("mysql:8.0")
        .WithDatabase("checkout")
        .WithUsername("checkout")
        .WithPassword("checkout")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public static readonly TwoC2POptions TwoC2P = new()
    {
        MerchantId = "JT01",
        SecretKey = TestSecret,
        BaseUrl = "https://fake-2c2p.test/payment/4.3/",
        BackendReturnUrl = "https://api.test/api/payments/2c2p/notification",
        FrontendReturnUrl = "https://api.test/api/payments/2c2p/return",
    };

    public FakePaymentProvider Payments { get; } = new(TwoC2P);

    /// <summary>Signs a 2C2P-style payload with <see cref="TestSecret"/> (as 2C2P would for a notification).</summary>
    public static string SignNotification(object payload) => new TwoC2PJwt(TestSecret).Sign(payload);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mySql.StartAsync(), _redis.StartAsync());
        _ = Server; // start the host now: applies migrations + seed
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _mySql.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MySql", _mySql.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Database:SkipMigrate", "false");
        builder.UseSetting("TwoC2P:MerchantId", TwoC2P.MerchantId);
        builder.UseSetting("TwoC2P:SecretKey", TwoC2P.SecretKey);
        builder.UseSetting("TwoC2P:BaseUrl", TwoC2P.BaseUrl);
        builder.UseSetting("TwoC2P:BackendReturnUrl", TwoC2P.BackendReturnUrl);
        builder.UseSetting("TwoC2P:FrontendReturnUrl", TwoC2P.FrontendReturnUrl);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(Payments);
        });
    }

    /// <summary>Same app and containers, with <see cref="IDistributedLock"/> always granting (simulates no lock protection).</summary>
    public WebApplicationFactory<Program> WithoutDistributedLock() =>
        WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, AlwaysGrantLock>();
        }));

    /// <summary>Empties transaction + customer_card, flushes Redis, resets the fake provider.</summary>
    public async Task ResetAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "SET FOREIGN_KEY_CHECKS = 0; TRUNCATE TABLE `transaction`; TRUNCATE TABLE customer_card; SET FOREIGN_KEY_CHECKS = 1;");
        }

        await using (var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString() + ",allowAdmin=true"))
            await redis.GetServer(redis.GetEndPoints()[0]).FlushAllDatabasesAsync();

        Payments.Reset();
    }

    /// <summary>Runs <paramref name="action"/> against a fresh CheckoutDbContext.</summary>
    public async Task<T> WithDbAsync<T>(Func<CheckoutDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<CheckoutDbContext>());
    }

    private sealed class AlwaysGrantLock : IDistributedLock
    {
        public Task<ILockHandle?> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken ct) =>
            Task.FromResult<ILockHandle?>(new Handle(resource));

        private sealed record Handle(string Resource) : ILockHandle
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
