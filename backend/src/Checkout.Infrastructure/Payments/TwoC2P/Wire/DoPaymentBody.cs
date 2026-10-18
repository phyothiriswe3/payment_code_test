using System.Text.Json.Serialization;

namespace Checkout.Infrastructure.Payments.TwoC2P.Wire;

/// <summary>
/// Plain (unsigned) JSON body for POST {base}/payment (Do Payment).
/// Per https://developer.2c2p.com/docs/api-do-payment-request-parameter and the v4.3 OpenAPI schema
/// https://developer.2c2p.com/reference/post_payment-4-3-payment (PaymentRequest: paymentToken, payment{code,data}, locale, clientID, responseReturnUrl).
/// Saved-card payment uses <c>payment.code.channelCode="CC"</c> + <c>payment.data.customerToken</c>
/// (https://developer.2c2p.com/docs/sdk-payment-with-customer-token).
/// <c>responseReturnUrl</c> is intentionally omitted: the docs reserve it for third-party UI vendors; merchants
/// rely on <c>frontendReturnUrl</c> from the Payment Token request.
/// </summary>
public sealed class DoPaymentBody
{
    [JsonPropertyName("paymentToken")] public required string PaymentToken { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("payment")] public required DoPaymentPayment Payment { get; init; }
}

public sealed class DoPaymentPayment
{
    [JsonPropertyName("code")] public required DoPaymentCode Code { get; init; }
    [JsonPropertyName("data")] public required DoPaymentData Data { get; init; }
}

public sealed class DoPaymentCode
{
    [JsonPropertyName("channelCode")] public required string ChannelCode { get; init; }
}

public sealed class DoPaymentData
{
    [JsonPropertyName("customerToken")] public string? CustomerToken { get; init; }
}

/// <summary>
/// Plain JSON response of Do Payment. Per https://developer.2c2p.com/docs/api-do-payment-response-parameter.
/// respCode 1001 = redirect required, <c>data</c> holds the 3DS/redirect URL; 2000 = transaction completed
/// (https://developer.2c2p.com/docs/response-code-payment-flow), confirm with Payment Inquiry.
/// </summary>
public sealed class DoPaymentResponse
{
    [JsonPropertyName("channelCode")] public string? ChannelCode { get; init; }
    [JsonPropertyName("invoiceNo")] public string? InvoiceNo { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("data")] public string? Data { get; init; }
    [JsonPropertyName("respCode")] public string? RespCode { get; init; }
    [JsonPropertyName("respDesc")] public string? RespDesc { get; init; }
}
