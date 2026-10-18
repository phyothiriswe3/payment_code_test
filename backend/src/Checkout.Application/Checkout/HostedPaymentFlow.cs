using Checkout.Application.Abstractions;
using Checkout.Application.Dtos;
using Checkout.Application.Payments;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Checkout.Application.Checkout;

/// <summary>Creates a 2C2P payment token and returns the hosted payment page URL.</summary>
public sealed class HostedPaymentFlow(
    IAppDbContext db,
    IPaymentProvider provider,
    IClock clock,
    ILogger<HostedPaymentFlow> logger) : IHostedPaymentFlow
{
    public async Task<CheckoutResponse> ExecuteAsync(PaymentTransaction tx, Product product, CancellationToken ct)
    {
        var recurring = product.PaymentType == PaymentType.Recurring;
        var request = new PaymentTokenRequest(
            InvoiceNo: tx.InvoiceNo,
            Description: product.Name,
            Amount: tx.Amount,
            CurrencyCode: tx.Currency,
            PaymentChannel: recurring ? ["CC"] : null,
            Tokenize: recurring,
            CustomerToken: null,
            CustomerTokenOnly: false,
            Request3DS: null);

        PaymentTokenResult result;
        try
        {
            result = await provider.CreatePaymentTokenAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Payment token request failed for invoice {InvoiceNo}", tx.InvoiceNo);
            MarkFailed(tx, "ERR", "Payment provider unavailable");
            await db.SaveChangesAsync(CancellationToken.None);
            return ToResponse(tx);
        }

        if (result.Success)
        {
            var now = clock.UtcNow;
            tx.PaymentToken = result.PaymentToken;
            tx.RedirectUrl = result.WebPaymentUrl;
            tx.RespCode = result.RespCode;
            tx.RespDesc = result.RespDesc;
            tx.ProcessedAt = now;
            tx.UpdatedAt = now;
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogInformation("Payment token created for invoice {InvoiceNo}", tx.InvoiceNo);
        }
        else
        {
            logger.LogWarning("Payment token rejected for invoice {InvoiceNo}: {RespCode} {RespDesc}",
                tx.InvoiceNo, result.RespCode, result.RespDesc);
            MarkFailed(tx, result.RespCode, result.RespDesc);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return ToResponse(tx);
    }

    private void MarkFailed(PaymentTransaction tx, string respCode, string respDesc)
    {
        var now = clock.UtcNow;
        if (tx.Status == TransactionStatus.Pending)
            tx.Status = TransactionStatus.Failed;
        tx.RespCode = respCode;
        tx.RespDesc = respDesc;
        tx.ProcessedAt = now;
        tx.UpdatedAt = now;
    }

    private static CheckoutResponse ToResponse(PaymentTransaction tx) =>
        new(tx.InvoiceNo, tx.Status.ToString(), tx.RedirectUrl);
}
