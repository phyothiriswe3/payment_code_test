using Checkout.Domain.Enums;

namespace Checkout.Domain.Entities;

/// <summary>Maps to table <c>transaction</c>.</summary>
public class PaymentTransaction
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? CustomerCardId { get; set; }
    public CustomerCard? CustomerCard { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    /// <summary>SHA-256 of customerId|productId|cardId; detects key reuse with a different body.</summary>
    public string RequestFingerprint { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SGD";
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public PaymentFlow Flow { get; set; }

    public string? PaymentToken { get; set; }
    public string? RedirectUrl { get; set; }

    public string? PaymentChannel { get; set; }
    public string? MaskedCardNo { get; set; }
    public string? ProviderRef { get; set; }
    public string? RespCode { get; set; }
    public string? RespDesc { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>Set once the checkout call to the provider has finished; idempotent replays only return rows with this set.</summary>
    public DateTime? ProcessedAt { get; set; }
}
