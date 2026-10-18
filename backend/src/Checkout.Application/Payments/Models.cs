namespace Checkout.Application.Payments;

public record PaymentTokenRequest(
    string InvoiceNo, string Description, decimal Amount, string CurrencyCode,
    string[]? PaymentChannel, bool Tokenize, string[]? CustomerToken, bool CustomerTokenOnly, string? Request3DS);

public record PaymentTokenResult(bool Success, string RespCode, string RespDesc, string? PaymentToken, string? WebPaymentUrl);

public record DoPaymentRequest(string PaymentToken, string ChannelCode, string CustomerToken);

/// <summary>RespCode 1001 means RedirectUrl holds the 3DS URL; 2000 means completed (confirm with inquiry).</summary>
public record DoPaymentResult(string RespCode, string RespDesc, string? RedirectUrl);

public record PaymentStatusResult(
    string InvoiceNo, decimal Amount, string CurrencyCode, string RespCode, string RespDesc,
    string? ChannelCode, string? AccountNo, string? TranRef, string? CustomerToken, DateTime? TransactionDateTime);
