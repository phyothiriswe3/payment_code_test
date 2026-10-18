using System.Text.Json.Serialization;

namespace Checkout.Infrastructure.Payments.TwoC2P.Wire;

/// <summary>
/// JWT payload for POST {base}/paymentToken.
/// Fields per https://developer.2c2p.com/docs/api-payment-token-request-parameter
/// (sample in https://developer.2c2p.com/docs/api-payment-token). Amount is D(12,5); sent with 2 dp for SGD.
/// Deviation: the docs page's encoded sample still shows legacy <c>cardTokens</c>/<c>cardTokenOnly</c>; the
/// parameter table (authoritative) names them <c>customerToken</c>/<c>customerTokenOnly</c>, which is what we send.
/// </summary>
public sealed class PaymentTokenPayload
{
    [JsonPropertyName("merchantID")] public required string MerchantId { get; init; }
    [JsonPropertyName("invoiceNo")] public required string InvoiceNo { get; init; }
    [JsonPropertyName("description")] public required string Description { get; init; }
    [JsonPropertyName("amount")] public required decimal Amount { get; init; }
    [JsonPropertyName("currencyCode")] public required string CurrencyCode { get; init; }
    [JsonPropertyName("paymentChannel")] public string[]? PaymentChannel { get; init; }
    /// <summary>"Y" (default) / "F" (force) / "N".</summary>
    [JsonPropertyName("request3DS")] public string? Request3DS { get; init; }
    [JsonPropertyName("tokenize")] public bool? Tokenize { get; init; }
    [JsonPropertyName("customerToken")] public string[]? CustomerToken { get; init; }
    [JsonPropertyName("customerTokenOnly")] public bool? CustomerTokenOnly { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("frontendReturnUrl")] public string? FrontendReturnUrl { get; init; }
    [JsonPropertyName("backendReturnUrl")] public string? BackendReturnUrl { get; init; }
}

/// <summary>
/// Decoded JWT response of POST {base}/paymentToken.
/// Per https://developer.2c2p.com/docs/api-payment-token ("Payment Token Response").
/// </summary>
public sealed class PaymentTokenResponse
{
    [JsonPropertyName("webPaymentUrl")] public string? WebPaymentUrl { get; init; }
    [JsonPropertyName("paymentToken")] public string? PaymentToken { get; init; }
    [JsonPropertyName("respCode")] public string? RespCode { get; init; }
    [JsonPropertyName("respDesc")] public string? RespDesc { get; init; }
}
