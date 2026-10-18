using Checkout.Application.Payments;

namespace Checkout.Application.Abstractions;

public interface IPaymentProvider
{
    Task<PaymentTokenResult> CreatePaymentTokenAsync(PaymentTokenRequest request, CancellationToken ct);
    Task<DoPaymentResult> DoPaymentAsync(DoPaymentRequest request, CancellationToken ct);
    Task<PaymentStatusResult> InquireAsync(string invoiceNo, CancellationToken ct);
    /// <summary>Verifies and decodes a backend notification. Throws InvalidSignatureException.</summary>
    PaymentStatusResult ParseNotification(string payloadJwt);
}
