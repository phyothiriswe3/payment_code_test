using Checkout.Application.Abstractions;
using Checkout.Application.Dtos;
using Checkout.Application.Payments;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Checkout.Application.Checkout;

/// <summary>
/// Charges a saved card (customer token): payment token → DoPayment. May return a 3DS redirect.
/// Never marks Succeeded directly; success is confirmed via inquiry in <see cref="IPaymentResultService"/>.
/// </summary>
public sealed class SavedCardPaymentFlow(
    IAppDbContext db,
    IPaymentProvider provider,
    IPaymentResultService results,
    IClock clock,
    ILogger<SavedCardPaymentFlow> logger) : ISavedCardPaymentFlow
{
    private const string CardChannel = "CC";

    public async Task<CheckoutResponse> ExecuteAsync(PaymentTransaction tx, Product product, CustomerCard card, CancellationToken ct)
    {
        var doPaymentStarted = false;
        try
        {
            var tokenResult = await provider.CreatePaymentTokenAsync(new PaymentTokenRequest(
                tx.InvoiceNo, product.Name, tx.Amount, tx.Currency,
                PaymentChannel: [CardChannel], Tokenize: false,
                CustomerToken: [card.CustomerToken], CustomerTokenOnly: true, Request3DS: "N"), ct);

            if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.PaymentToken))
            {
                logger.LogWarning("Payment token failed for {InvoiceNo}: {RespCode}", tx.InvoiceNo, tokenResult.RespCode);
                return await FailAsync(tx, tokenResult.RespCode, tokenResult.RespDesc);
            }

            tx.PaymentToken = tokenResult.PaymentToken;
            Touch(tx);
            await db.SaveChangesAsync(ct);

            doPaymentStarted = true;
            var pay = await provider.DoPaymentAsync(
                new DoPaymentRequest(tokenResult.PaymentToken, CardChannel, card.CustomerToken), ct);

            switch (pay.RespCode)
            {
                case PaymentResponseCodes.RedirectRequired:
                    tx.RedirectUrl = pay.RedirectUrl;
                    SetResp(tx, pay.RespCode, pay.RespDesc);
                    tx.ProcessedAt = clock.UtcNow;
                    Touch(tx);
                    await db.SaveChangesAsync(CancellationToken.None);
                    return new CheckoutResponse(tx.InvoiceNo, nameof(TransactionStatus.Pending), pay.RedirectUrl);

                case PaymentResponseCodes.InquireForResult:
                    SetResp(tx, pay.RespCode, pay.RespDesc);
                    tx.ProcessedAt = clock.UtcNow;
                    Touch(tx);
                    await db.SaveChangesAsync(CancellationToken.None);
                    var dto = await results.ConfirmAsync(tx.InvoiceNo, ct);
                    return new CheckoutResponse(tx.InvoiceNo, dto.Status, null);

                case var code when PaymentResponseCodes.IsNonFinal(code):
                    // No final outcome yet and a charge may have happened: leave Pending; /confirm settles it.
                    logger.LogWarning("DoPayment not final for {InvoiceNo}: {RespCode}; leaving Pending", tx.InvoiceNo, pay.RespCode);
                    SetResp(tx, pay.RespCode, pay.RespDesc);
                    return await LeavePendingAsync(tx);

                default:
                    logger.LogWarning("DoPayment declined for {InvoiceNo}: {RespCode}", tx.InvoiceNo, pay.RespCode);
                    return await FailAsync(tx, pay.RespCode, pay.RespDesc);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !doPaymentStarted)
        {
            if (!doPaymentStarted)
            {
                logger.LogError(ex, "Payment provider error before charge for {InvoiceNo}", tx.InvoiceNo);
                return await FailAsync(tx, "ERR", "Payment provider unavailable");
            }

            // A charge may have happened: leave Pending; /confirm inquiry resolves the real outcome.
            logger.LogError(ex, "Payment provider error after DoPayment for {InvoiceNo}; leaving Pending", tx.InvoiceNo);
            return await LeavePendingAsync(tx);
        }
        catch (OperationCanceledException)
        {
            // Cancelled after DoPayment started: same as above, never assume failure.
            return await LeavePendingAsync(tx);
        }
    }

    private async Task<CheckoutResponse> LeavePendingAsync(PaymentTransaction tx)
    {
        tx.ProcessedAt ??= clock.UtcNow;
        Touch(tx);
        await db.SaveChangesAsync(CancellationToken.None);
        return new CheckoutResponse(tx.InvoiceNo, nameof(TransactionStatus.Pending), null);
    }

    private async Task<CheckoutResponse> FailAsync(PaymentTransaction tx, string? respCode, string? respDesc)
    {
        tx.Status = TransactionStatus.Failed;
        SetResp(tx, respCode, respDesc);
        tx.ProcessedAt = clock.UtcNow;
        Touch(tx);
        await db.SaveChangesAsync(CancellationToken.None);
        return new CheckoutResponse(tx.InvoiceNo, nameof(TransactionStatus.Failed), null);
    }

    private static void SetResp(PaymentTransaction tx, string? code, string? desc)
    {
        tx.RespCode = code;
        tx.RespDesc = desc;
    }

    private void Touch(PaymentTransaction tx) => tx.UpdatedAt = clock.UtcNow;
}
