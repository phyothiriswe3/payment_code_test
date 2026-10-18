using Checkout.Application.Catalog;
using Checkout.Application.Checkout;
using Checkout.Application.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace Checkout.Application;

public static class DependencyInjection
{
    /// <summary>Registers application services (scoped) and binds <see cref="CheckoutOptions"/> from "App".</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<CheckoutOptions>().BindConfiguration(CheckoutOptions.SectionName);

        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ITransactionQueryService, TransactionQueryService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IHostedPaymentFlow, HostedPaymentFlow>();
        services.AddScoped<ISavedCardPaymentFlow, SavedCardPaymentFlow>();
        services.AddScoped<IPaymentResultService, PaymentResultService>();
        return services;
    }
}
