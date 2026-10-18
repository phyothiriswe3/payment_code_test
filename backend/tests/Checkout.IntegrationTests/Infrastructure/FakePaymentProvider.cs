using Checkout.Application.Abstractions;
using Checkout.Application.Payments;
using Checkout.Infrastructure.Payments.TwoC2P;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Checkout.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory 2C2P stand-in. Outbound calls are scripted per test (handlers + delay) and counted;
/// <see cref="ParseNotification"/> delegates to the real provider, so signatures are really verified.
/// Call <see cref="Reset"/> between tests (done by <see cref="ApiFactory.ResetAsync"/>).
/// </summary>
public sealed class FakePaymentProvider : IPaymentProvider
{
    private readonly TwoC2PPaymentProvider _real;
    private int _tokenCalls;
    private int _doPaymentCalls;
    private int _inquiryCalls;

    public FakePaymentProvider(TwoC2POptions options)
    {
        // Only ParseNotification is used on the real provider; its HttpClient never sends.
        _real = new TwoC2PPaymentProvider(new HttpClient(), Options.Create(options), NullLogger<TwoC2PPaymentProvider>.Instance);
        Reset();
    }

    public int TokenCalls => Volatile.Read(ref _tokenCalls);
    public int DoPaymentCalls => Volatile.Read(ref _doPaymentCalls);
    public int InquiryCalls => Volatile.Read(ref _inquiryCalls);

    /// <summary>Delay applied inside CreatePaymentTokenAsync (default 300 ms) to widen race windows.</summary>
    public TimeSpan TokenDelay { get; set; }

    public Func<PaymentTokenRequest, PaymentTokenResult> OnCreateToken { get; set; } = null!;
    public Func<DoPaymentRequest, DoPaymentResult> OnDoPayment { get; set; } = null!;
    public Func<string, PaymentStatusResult> OnInquire { get; set; } = null!;

    public static string FakeWebPaymentUrl(string invoiceNo) => $"https://fake-2c2p.test/pay/{invoiceNo}";

    public void Reset()
    {
        Interlocked.Exchange(ref _tokenCalls, 0);
        Interlocked.Exchange(ref _doPaymentCalls, 0);
        Interlocked.Exchange(ref _inquiryCalls, 0);
        TokenDelay = TimeSpan.FromMilliseconds(300);
        OnCreateToken = req => new PaymentTokenResult(true, "0000", "Success", $"tok-{req.InvoiceNo}", FakeWebPaymentUrl(req.InvoiceNo));
        OnDoPayment = _ => new DoPaymentResult("2000", "Transaction is completed, please do payment inquiry request for full payment information.", null);
        OnInquire = invoiceNo => throw new InvalidOperationException($"No inquiry result scripted for {invoiceNo}.");
    }

    public async Task<PaymentTokenResult> CreatePaymentTokenAsync(PaymentTokenRequest request, CancellationToken ct)
    {
        Interlocked.Increment(ref _tokenCalls);
        await Task.Delay(TokenDelay, ct);
        return OnCreateToken(request);
    }

    public Task<DoPaymentResult> DoPaymentAsync(DoPaymentRequest request, CancellationToken ct)
    {
        Interlocked.Increment(ref _doPaymentCalls);
        return Task.FromResult(OnDoPayment(request));
    }

    public Task<PaymentStatusResult> InquireAsync(string invoiceNo, CancellationToken ct)
    {
        Interlocked.Increment(ref _inquiryCalls);
        return Task.FromResult(OnInquire(invoiceNo));
    }

    public PaymentStatusResult ParseNotification(string payloadJwt) => _real.ParseNotification(payloadJwt);
}
