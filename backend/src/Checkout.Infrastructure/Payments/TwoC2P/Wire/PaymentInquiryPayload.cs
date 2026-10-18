using System.Text.Json.Serialization;

namespace Checkout.Infrastructure.Payments.TwoC2P.Wire;

/// <summary>
/// JWT payload for POST {base}/paymentInquiry.
/// Per https://developer.2c2p.com/docs/api-payment-inquiry and
/// https://developer.2c2p.com/docs/api-payment-inquiry-request-parameter (merchantID + invoiceNo + locale variant).
/// </summary>
public sealed class PaymentInquiryPayload
{
    [JsonPropertyName("merchantID")] public required string MerchantId { get; init; }
    [JsonPropertyName("invoiceNo")] public required string InvoiceNo { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
}

/// <summary>
/// Decoded JWT payload of both the Payment Inquiry response and the backend notification (backendReturnUrl).
/// Per https://developer.2c2p.com/docs/api-payment-inquiry-response-parameter and
/// https://developer.2c2p.com/docs/api-payment-response-back-end-parameter.
/// <c>transactionDateTime</c> is documented as <c>yyyyMMddHHmmss</c> (N 14); <c>accountNo</c> is the masked PAN (first 6 + last 4).
/// </summary>
public sealed class PaymentResultPayload
{
    [JsonPropertyName("merchantID")] public string? MerchantId { get; init; }
    [JsonPropertyName("invoiceNo")] public string? InvoiceNo { get; init; }
    [JsonPropertyName("amount")] public decimal? Amount { get; init; }
    [JsonPropertyName("currencyCode")] public string? CurrencyCode { get; init; }
    /// <summary>
    /// When the merchant settles in another currency (e.g. sandbox JT01 settles in THB), 2C2P converts:
    /// <c>amount</c>/<c>currencyCode</c> become the settlement amount and the originally requested
    /// amount/currency are returned as <c>fxAmount</c>/<c>fxCurrencyCode</c>.
    /// </summary>
    [JsonPropertyName("fxAmount")] public decimal? FxAmount { get; init; }
    [JsonPropertyName("fxCurrencyCode")] public string? FxCurrencyCode { get; init; }
    [JsonPropertyName("transactionDateTime")] public string? TransactionDateTime { get; init; }
    [JsonPropertyName("agentCode")] public string? AgentCode { get; init; }
    [JsonPropertyName("channelCode")] public string? ChannelCode { get; init; }
    [JsonPropertyName("approvalCode")] public string? ApprovalCode { get; init; }
    [JsonPropertyName("referenceNo")] public string? ReferenceNo { get; init; }
    [JsonPropertyName("tranRef")] public string? TranRef { get; init; }
    [JsonPropertyName("accountNo")] public string? AccountNo { get; init; }
    [JsonPropertyName("customerToken")] public string? CustomerToken { get; init; }
    [JsonPropertyName("customerTokenExpiry")] public string? CustomerTokenExpiry { get; init; }
    [JsonPropertyName("respCode")] public string? RespCode { get; init; }
    [JsonPropertyName("respDesc")] public string? RespDesc { get; init; }
}
