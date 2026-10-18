using System.Text.Json.Serialization;

namespace Checkout.Infrastructure.Payments.TwoC2P.Wire;

/// <summary>
/// Request wrapper <c>{"payload":"&lt;jwt&gt;"}</c> for JWT endpoints (paymentToken, paymentInquiry).
/// Per the v4.3 OpenAPI <c>PayloadRequest</c> schema (https://developer.2c2p.com/reference/post_payment-4-3-paymenttoken).
/// </summary>
public sealed class PayloadRequest
{
    [JsonPropertyName("payload")] public required string Payload { get; init; }
}

/// <summary>
/// Response wrapper: success is a signed <c>{"payload":"&lt;jwt&gt;"}</c>
/// (https://developer.2c2p.com/docs/api-payment-token "Payment Token Response"); request-level errors come back
/// unsigned as <c>{"respCode":"9xxx","respDesc":"..."}</c> (https://developer.2c2p.com/docs/response-code-payment).
/// </summary>
public sealed class ResponseEnvelope
{
    [JsonPropertyName("payload")] public string? Payload { get; init; }
    [JsonPropertyName("respCode")] public string? RespCode { get; init; }
    [JsonPropertyName("respDesc")] public string? RespDesc { get; init; }
}
