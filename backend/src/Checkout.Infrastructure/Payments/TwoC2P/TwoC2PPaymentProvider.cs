using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Checkout.Application.Abstractions;
using Checkout.Application.Errors;
using Checkout.Application.Payments;
using Checkout.Infrastructure.Payments.TwoC2P.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Checkout.Infrastructure.Payments.TwoC2P;

/// <summary>
/// 2C2P PGW v4.3 client. JWT endpoints (paymentToken, paymentInquiry) take and return <c>{payload}</c>;
/// Do Payment (payment) is plain JSON. Never logs the secret, payment/customer tokens, or card data.
/// </summary>
public sealed class TwoC2PPaymentProvider : IPaymentProvider
{
    public const string SuccessCode = "0000";
    public const string RedirectCode = "1001";
    private const string Locale = "en";

    private static readonly string[] DateTimeFormats = ["yyyyMMddHHmmss", "ddMMyyHHmmss"];

    private readonly HttpClient _http;
    private readonly TwoC2POptions _options;
    private readonly TwoC2PJwt _jwt;
    private readonly ILogger<TwoC2PPaymentProvider> _logger;

    public TwoC2PPaymentProvider(HttpClient http, IOptions<TwoC2POptions> options, ILogger<TwoC2PPaymentProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _jwt = new TwoC2PJwt(_options.SecretKey);
        _logger = logger;
        _http.BaseAddress ??= new Uri(EnsureTrailingSlash(_options.BaseUrl));
    }

    public async Task<PaymentTokenResult> CreatePaymentTokenAsync(PaymentTokenRequest req, CancellationToken ct)
    {
        var payload = new PaymentTokenPayload
        {
            MerchantId = _options.MerchantId,
            InvoiceNo = req.InvoiceNo,
            Description = req.Description,
            Amount = ToTwoDecimals(req.Amount),
            CurrencyCode = req.CurrencyCode,
            PaymentChannel = req.PaymentChannel is { Length: > 0 } ? req.PaymentChannel : null,
            Request3DS = string.IsNullOrEmpty(req.Request3DS) ? null : req.Request3DS,
            Tokenize = req.Tokenize ? true : null,
            CustomerToken = req.CustomerToken is { Length: > 0 } ? req.CustomerToken : null,
            CustomerTokenOnly = req.CustomerTokenOnly ? true : null,
            Locale = Locale,
            FrontendReturnUrl = BuildFrontendReturnUrl(req.InvoiceNo),
            BackendReturnUrl = _options.BackendReturnUrl,
        };

        var envelope = await PostSignedAsync("paymentToken", payload, ct);
        if (string.IsNullOrEmpty(envelope.Payload))
        {
            var code = envelope.RespCode ?? string.Empty;
            _logger.LogWarning("2C2P paymentToken rejected for {InvoiceNo}: {RespCode} {RespDesc}", req.InvoiceNo, code, envelope.RespDesc);
            return new PaymentTokenResult(false, code, envelope.RespDesc ?? string.Empty, null, null);
        }

        var response = Deserialize<PaymentTokenResponse>(_jwt.Verify(envelope.Payload));
        var respCode = response.RespCode ?? string.Empty;
        var success = respCode == SuccessCode;
        _logger.LogInformation("2C2P paymentToken for {InvoiceNo}: {RespCode}", req.InvoiceNo, respCode);
        return new PaymentTokenResult(success, respCode, response.RespDesc ?? string.Empty,
            success ? response.PaymentToken : null, success ? response.WebPaymentUrl : null);
    }

    public async Task<DoPaymentResult> DoPaymentAsync(DoPaymentRequest req, CancellationToken ct)
    {
        var body = new DoPaymentBody
        {
            PaymentToken = req.PaymentToken,
            Locale = Locale,
            Payment = new DoPaymentPayment
            {
                Code = new DoPaymentCode { ChannelCode = req.ChannelCode },
                Data = new DoPaymentData { CustomerToken = req.CustomerToken },
            },
        };

        using var httpResponse = await _http.PostAsJsonAsync("payment", body, TwoC2PJwt.JsonOptions, ct);
        httpResponse.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await httpResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        // Documented as plain JSON; accept a signed {payload} too in case the merchant profile wraps it.
        var root = doc.RootElement;
        if (root.TryGetProperty("payload", out var signed) && signed.ValueKind == JsonValueKind.String)
            root = _jwt.Verify(signed.GetString()!);

        var response = Deserialize<DoPaymentResponse>(root);
        var respCode = response.RespCode ?? string.Empty;
        _logger.LogInformation("2C2P payment (Do Payment) result: {RespCode}", respCode);
        return new DoPaymentResult(respCode, response.RespDesc ?? string.Empty,
            respCode == RedirectCode ? response.Data : null);
    }

    public async Task<PaymentStatusResult> InquireAsync(string invoiceNo, CancellationToken ct)
    {
        var payload = new PaymentInquiryPayload { MerchantId = _options.MerchantId, InvoiceNo = invoiceNo, Locale = Locale };
        var envelope = await PostSignedAsync("paymentInquiry", payload, ct);
        if (string.IsNullOrEmpty(envelope.Payload))
        {
            // Unsigned request-level error: report the code; amount/currency are unknown.
            _logger.LogWarning("2C2P paymentInquiry rejected for {InvoiceNo}: {RespCode} {RespDesc}", invoiceNo, envelope.RespCode, envelope.RespDesc);
            return new PaymentStatusResult(invoiceNo, 0m, string.Empty, envelope.RespCode ?? string.Empty,
                envelope.RespDesc ?? string.Empty, null, null, null, null, null);
        }

        var result = Map(_jwt.Verify(envelope.Payload));
        _logger.LogInformation("2C2P paymentInquiry for {InvoiceNo}: {RespCode}", invoiceNo, result.RespCode);
        return result;
    }

    public PaymentStatusResult ParseNotification(string payloadJwt) => Map(_jwt.Verify(payloadJwt));

    private async Task<ResponseEnvelope> PostSignedAsync(string path, object payload, CancellationToken ct)
    {
        var request = new PayloadRequest { Payload = _jwt.Sign(payload) };
        using var httpResponse = await _http.PostAsJsonAsync(path, request, TwoC2PJwt.JsonOptions, ct);
        httpResponse.EnsureSuccessStatusCode();
        return await httpResponse.Content.ReadFromJsonAsync<ResponseEnvelope>(TwoC2PJwt.JsonOptions, ct)
               ?? throw new InvalidOperationException($"2C2P {path} returned an empty response.");
    }

    private static PaymentStatusResult Map(JsonElement payload)
    {
        var p = Deserialize<PaymentResultPayload>(payload);
        // Report the amount in the currency we requested: with FX conversion 2C2P moves it to fxAmount/fxCurrencyCode.
        var converted = p.FxAmount > 0
                        && !string.IsNullOrEmpty(p.FxCurrencyCode)
                        && !string.Equals(p.FxCurrencyCode, p.CurrencyCode, StringComparison.OrdinalIgnoreCase);
        return new PaymentStatusResult(
            p.InvoiceNo ?? string.Empty,
            converted ? p.FxAmount!.Value : p.Amount ?? 0m,
            converted ? p.FxCurrencyCode! : p.CurrencyCode ?? string.Empty,
            p.RespCode ?? string.Empty,
            p.RespDesc ?? string.Empty,
            NullIfEmpty(p.ChannelCode),
            NullIfEmpty(p.AccountNo),
            NullIfEmpty(p.TranRef),
            NullIfEmpty(p.CustomerToken),
            ParseDateTime(p.TransactionDateTime));
    }

    private static T Deserialize<T>(JsonElement element) where T : class
    {
        try
        {
            return element.Deserialize<T>(TwoC2PJwt.JsonOptions)
                   ?? throw new InvalidSignatureException("Empty 2C2P payload.");
        }
        catch (JsonException)
        {
            throw new InvalidSignatureException("Unreadable 2C2P payload.");
        }
    }

    /// <summary>
    /// 2C2P sends <c>yyyyMMddHHmmss</c> without a zone (merchant-local time). Returned as-is with
    /// <see cref="DateTimeKind.Unspecified"/>; callers needing UTC should use their own clock for PaidAt.
    /// </summary>
    private static DateTime? ParseDateTime(string? value) =>
        DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt
            : null;

    private string BuildFrontendReturnUrl(string invoiceNo)
    {
        var baseUrl = _options.FrontendReturnUrl;
        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}invoiceNo={Uri.EscapeDataString(invoiceNo)}";
    }

    /// <summary>Round to cents and force scale 2 so the JSON number is written as e.g. 25.00.</summary>
    private static decimal ToTwoDecimals(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero) + 0.00m;

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
