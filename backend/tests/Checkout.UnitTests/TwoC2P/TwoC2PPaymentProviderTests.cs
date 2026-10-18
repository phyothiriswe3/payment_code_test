using System.Net;
using System.Text;
using System.Text.Json;
using Checkout.Application.Errors;
using Checkout.Application.Payments;
using Checkout.Infrastructure.Payments.TwoC2P;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Checkout.UnitTests.TwoC2P;

public class TwoC2PPaymentProviderTests
{
    private const string Secret = "unit-test-secret-not-a-real-key";
    private static readonly TwoC2PJwt Jwt = new(Secret);

    private static readonly TwoC2POptions Options = new()
    {
        MerchantId = "JT01",
        SecretKey = Secret,
        BaseUrl = "https://sandbox-pgw.2c2p.com/payment/4.3/",
        BackendReturnUrl = "https://tunnel.example/api/payments/2c2p/notification",
        FrontendReturnUrl = "https://tunnel.example/api/payments/2c2p/return",
    };

    private static (TwoC2PPaymentProvider Provider, StubHandler Handler) Create(string responseJson)
    {
        var handler = new StubHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri(Options.BaseUrl) };
        return (new TwoC2PPaymentProvider(http, Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<TwoC2PPaymentProvider>.Instance), handler);
    }

    private static string Signed(object payload) => JsonSerializer.Serialize(new { payload = Jwt.Sign(payload) });

    private static PaymentTokenRequest TokenRequest(bool tokenize = false) =>
        new("INV20261017000000123456", "Day Pass", 25m, "SGD", ["CC"], tokenize, null, false, null);

    [Fact]
    public async Task CreatePaymentToken_success_returns_token_and_url_and_sends_expected_payload()
    {
        var (provider, handler) = Create(Signed(new
        {
            webPaymentUrl = "https://sandbox-pgw-ui.2c2p.com/payment/4.3/#/token/abc",
            paymentToken = "tok-abc",
            respCode = "0000",
            respDesc = "Success",
        }));

        var result = await provider.CreatePaymentTokenAsync(TokenRequest(tokenize: true), CancellationToken.None);

        result.Should().Be(new PaymentTokenResult(true, "0000", "Success", "tok-abc",
            "https://sandbox-pgw-ui.2c2p.com/payment/4.3/#/token/abc"));

        handler.RequestUri!.ToString().Should().Be("https://sandbox-pgw.2c2p.com/payment/4.3/paymentToken");
        var jwt = JsonDocument.Parse(handler.RequestBody!).RootElement.GetProperty("payload").GetString()!;
        var sent = Jwt.Verify(jwt);
        sent.GetProperty("merchantID").GetString().Should().Be("JT01");
        sent.GetProperty("invoiceNo").GetString().Should().Be("INV20261017000000123456");
        sent.GetProperty("currencyCode").GetString().Should().Be("SGD");
        sent.GetProperty("amount").ValueKind.Should().Be(JsonValueKind.Number);
        sent.GetProperty("amount").GetRawText().Should().Be("25.00");
        sent.GetProperty("paymentChannel").EnumerateArray().Select(e => e.GetString()).Should().Equal("CC");
        sent.GetProperty("tokenize").GetBoolean().Should().BeTrue();
        sent.GetProperty("frontendReturnUrl").GetString()
            .Should().Be("https://tunnel.example/api/payments/2c2p/return?invoiceNo=INV20261017000000123456");
        sent.GetProperty("backendReturnUrl").GetString().Should().Be(Options.BackendReturnUrl);
        sent.TryGetProperty("customerToken", out _).Should().BeFalse("nulls are omitted");
        sent.TryGetProperty("request3DS", out _).Should().BeFalse("nulls are omitted");
    }

    [Fact]
    public async Task CreatePaymentToken_unsigned_error_maps_to_failure()
    {
        var (provider, _) = Create("""{"respCode":"9042","respDesc":"Invalid merchant."}""");

        var result = await provider.CreatePaymentTokenAsync(TokenRequest(), CancellationToken.None);

        result.Should().Be(new PaymentTokenResult(false, "9042", "Invalid merchant.", null, null));
    }

    [Fact]
    public async Task DoPayment_1001_returns_redirect_url_from_data()
    {
        var (provider, handler) = Create(
            """{"channelCode":"VI","data":"https://3ds.example/challenge","respCode":"1001","respDesc":"Redirect is required."}""");

        var result = await provider.DoPaymentAsync(new DoPaymentRequest("tok-abc", "CC", "cust-tok-1"), CancellationToken.None);

        result.Should().Be(new DoPaymentResult("1001", "Redirect is required.", "https://3ds.example/challenge"));
        handler.RequestUri!.ToString().Should().Be("https://sandbox-pgw.2c2p.com/payment/4.3/payment");
        var body = JsonDocument.Parse(handler.RequestBody!).RootElement;
        body.GetProperty("paymentToken").GetString().Should().Be("tok-abc");
        body.GetProperty("payment").GetProperty("code").GetProperty("channelCode").GetString().Should().Be("CC");
        body.GetProperty("payment").GetProperty("data").GetProperty("customerToken").GetString().Should().Be("cust-tok-1");
    }

    [Fact]
    public async Task DoPayment_2000_has_no_redirect_url()
    {
        var (provider, _) = Create(
            """{"channelCode":"VI","invoiceNo":"INV1","respCode":"2000","respDesc":"Transaction is completed, please do payment inquiry request for full payment information."}""");

        var result = await provider.DoPaymentAsync(new DoPaymentRequest("tok-abc", "CC", "cust-tok-1"), CancellationToken.None);

        result.RespCode.Should().Be("2000");
        result.RedirectUrl.Should().BeNull();
    }

    [Fact]
    public async Task Inquire_maps_signed_response()
    {
        var (provider, handler) = Create(Signed(new
        {
            merchantID = "JT01", invoiceNo = "INV1", amount = 25.00m, currencyCode = "SGD",
            transactionDateTime = "20261017123456", channelCode = "VI", accountNo = "411111XXXXXX1111",
            tranRef = "123", customerToken = "cust-tok-1", respCode = "0000", respDesc = "Success",
        }));

        var result = await provider.InquireAsync("INV1", CancellationToken.None);

        result.Should().Be(new PaymentStatusResult("INV1", 25.00m, "SGD", "0000", "Success", "VI",
            "411111XXXXXX1111", "123", "cust-tok-1", new DateTime(2026, 10, 17, 12, 34, 56)));
        handler.RequestUri!.ToString().Should().Be("https://sandbox-pgw.2c2p.com/payment/4.3/paymentInquiry");
        var sent = Jwt.Verify(JsonDocument.Parse(handler.RequestBody!).RootElement.GetProperty("payload").GetString()!);
        sent.GetProperty("merchantID").GetString().Should().Be("JT01");
        sent.GetProperty("invoiceNo").GetString().Should().Be("INV1");
        sent.GetProperty("locale").GetString().Should().Be("en");
    }

    [Fact]
    public async Task Inquire_with_bad_signature_throws()
    {
        var forged = new TwoC2PJwt("attacker-secret").Sign(new { invoiceNo = "INV1", respCode = "0000", amount = 25.00m });
        var (provider, _) = Create(JsonSerializer.Serialize(new { payload = forged }));

        var act = () => provider.InquireAsync("INV1", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSignatureException>();
    }

    [Fact]
    public void ParseNotification_maps_fields_and_rejects_tampering()
    {
        var (provider, _) = Create("{}");
        var token = Jwt.Sign(new
        {
            merchantID = "JT01", invoiceNo = "INV1", amount = 99.00m, currencyCode = "SGD",
            transactionDateTime = "20261017080910", channelCode = "MA", accountNo = "555555XXXXXX4444",
            tranRef = "987", customerToken = "cust-tok-2", respCode = "0000", respDesc = "Success",
        });

        var result = provider.ParseNotification(token);

        result.Should().Be(new PaymentStatusResult("INV1", 99.00m, "SGD", "0000", "Success", "MA",
            "555555XXXXXX4444", "987", "cust-tok-2", new DateTime(2026, 10, 17, 8, 9, 10)));

        var parts = token.Split('.');
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode("""{"invoiceNo":"INV1","amount":1}""")}.{parts[2]}";
        provider.Invoking(p => p.ParseNotification(tampered)).Should().Throw<InvalidSignatureException>();
    }

    [Fact]
    public void ParseNotification_with_fx_conversion_reports_requested_amount_and_currency()
    {
        // Shape observed from sandbox JT01, which settles in THB and converts the requested SGD amount.
        var (provider, _) = Create("{}");
        var token = Jwt.Sign(new
        {
            merchantID = "JT01", invoiceNo = "INV1", amount = 605.88m, currencyCode = "THB",
            fxAmount = 25.0m, fxRate = 24.2354868m, fxCurrencyCode = "SGD", respCode = "0000", respDesc = "Success"
        });

        var result = provider.ParseNotification(token);

        result.Amount.Should().Be(25.00m);
        result.CurrencyCode.Should().Be("SGD");
    }

    [Fact]
    public void ParseNotification_with_zero_fx_fields_uses_plain_amount()
    {
        var (provider, _) = Create("{}");
        var token = Jwt.Sign(new
        {
            merchantID = "JT01", invoiceNo = "INV1", amount = 25.00m, currencyCode = "SGD",
            fxAmount = 0m, fxCurrencyCode = "SGD", respCode = "0000", respDesc = "Success"
        });

        var result = provider.ParseNotification(token);

        result.Amount.Should().Be(25.00m);
        result.CurrencyCode.Should().Be("SGD");
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
