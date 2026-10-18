using Checkout.Application.Dtos;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;

namespace Checkout.Application;

public interface ICheckoutService
{
    Task<CheckoutResponse> CheckoutAsync(CheckoutCommand command, CancellationToken ct);
}

public interface IPaymentResultService
{
    Task ApplyNotificationAsync(string payloadJwt, CancellationToken ct);
    Task<TransactionDto> ConfirmAsync(string invoiceNo, CancellationToken ct);
}

public interface ICatalogService
{
    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken ct);
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct);
    Task<IReadOnlyList<CardDto>> GetCardsAsync(int customerId, CancellationToken ct);
}

public interface ITransactionQueryService
{
    /// <summary>Always reads fresh from the database; never cached.</summary>
    Task<TransactionDto?> GetAsync(string invoiceNo, CancellationToken ct);

    /// <summary>One page ordered by CreatedAt desc, then Id desc; always reads fresh from the database.</summary>
    Task<PagedResult<TransactionDto>> ListAsync(int page, int pageSize, TransactionStatus? status, CancellationToken ct);
}

/// <summary>Runs the provider calls for a freshly inserted Pending transaction and must set ProcessedAt before returning.</summary>
public interface IHostedPaymentFlow
{
    Task<CheckoutResponse> ExecuteAsync(PaymentTransaction tx, Product product, CancellationToken ct);
}

/// <summary>Runs the provider calls for a saved-card payment and must set ProcessedAt before returning.</summary>
public interface ISavedCardPaymentFlow
{
    Task<CheckoutResponse> ExecuteAsync(PaymentTransaction tx, Product product, CustomerCard card, CancellationToken ct);
}
