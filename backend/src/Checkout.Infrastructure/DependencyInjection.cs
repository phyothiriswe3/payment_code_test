using Checkout.Application.Abstractions;
using Checkout.Infrastructure.Common;
using Checkout.Infrastructure.Payments.TwoC2P;
using Checkout.Infrastructure.Persistence;
using Checkout.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Checkout.Infrastructure;

public static class DependencyInjection
{
    // Fixed version: AutoDetect would open a connection while building the container.
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var mySql = config.GetConnectionString("MySql")
                    ?? throw new InvalidOperationException("ConnectionStrings:MySql is not configured.");
        var redis = config.GetConnectionString("Redis")
                    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        // No EnableRetryOnFailure: the execution strategy rejects user-initiated transactions.
        services.AddDbContext<CheckoutDbContext>(o => o.UseMySql(mySql, ServerVersion));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CheckoutDbContext>());

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        services.AddSingleton<ICacheService, RedisCacheService>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IInvoiceNumberGenerator, InvoiceNumberGenerator>();

        services.AddOptions<TwoC2POptions>()
            .Bind(config.GetSection(TwoC2POptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<TwoC2PJwt>();
        services.AddHttpClient<IPaymentProvider, TwoC2PPaymentProvider>((sp, http) =>
        {
            var baseUrl = sp.GetRequiredService<IOptions<TwoC2POptions>>().Value.BaseUrl;
            http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
