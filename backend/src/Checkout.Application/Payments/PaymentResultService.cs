using Checkout.Application.Abstractions;
using Checkout.Application.Catalog;
using Checkout.Application.Dtos;
using Checkout.Application.Errors;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Checkout.Application.Payments;

/// <summary>
/// Applies 2C2P payment results (backend notification or Payment Inquiry) to the transaction row.
/// The conditional <c>WHERE status = Pending</c> update is the idempotency guard: the first final result wins,
/// later duplicates or conflicting results are no-ops.
/// </summary>
public sealed class PaymentResultService(
    IAppDbContext db,
    IPaymentProvider provider,
    ICacheService cache,
    ITransactionQueryService query,
    IClock clock,
    ILogger<PaymentResultService> logger) : IPaymentResultService
{
    // Column lengths (see persistence configuration).
    private const int ChannelMaxLength = 20;
    private const int MaskedCardMaxLength = 30;
    private const int ProviderRefMaxLength = 50;
    private const int RespCodeMaxLength = 10;
    private const int RespDescMaxLength = 100;

    public async Task ApplyNotificationAsync(string payloadJwt, CancellationToken ct)
    {
        // Throws InvalidSignatureException on a bad signature; the API maps it to 400.
        var result = provider.ParseNotification(payloadJwt);
        await ApplyAsync(result, ct);
    }

    public async Task<TransactionDto> ConfirmAsync(string invoiceNo, CancellationToken ct)
    {
        var status = await db.Transactions.AsNoTracking()
            .Where(t => t.InvoiceNo == invoiceNo)
            .Select(t => (TransactionStatus?)t.Status)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Transaction '{invoiceNo}' was not found.");

        if (status == TransactionStatus.Pending)
        {
            var result = await provider.InquireAsync(invoiceNo, ct);
            await ApplyAsync(result, ct);
        }

        return await query.GetAsync(invoiceNo, ct)
               ?? throw new NotFoundException($"Transaction '{invoiceNo}' was not found.");
    }

    private async Task ApplyAsync(PaymentStatusResult result, CancellationToken ct)
    {
        var tx = await db.Transactions.AsNoTracking()
            .Where(t => t.InvoiceNo == result.InvoiceNo)
            .Select(t => new { t.Id, t.CustomerId, t.Amount, t.Currency })
            .FirstOrDefaultAsync(ct);

        if (tx is null)
        {
            logger.LogWarning("Payment result for unknown invoice {InvoiceNo} ignored (respCode {RespCode})",
                result.InvoiceNo, result.RespCode);
            return;
        }

        if (decimal.Round(result.Amount, 2) != decimal.Round(tx.Amount, 2)
            || !string.Equals(result.CurrencyCode, tx.Currency, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "SECURITY: payment result for {InvoiceNo} does not match the transaction (amount {ResultAmount} {ResultCurrency} vs expected {Amount} {Currency}); ignored",
                result.InvoiceNo, result.Amount, result.CurrencyCode, tx.Amount, tx.Currency);
            return;
        }

        var respCode = result.RespCode?.Trim() ?? string.Empty;
        var now = clock.UtcNow;
        var code = Truncate(respCode, RespCodeMaxLength);
        var desc = Truncate(result.RespDesc, RespDescMaxLength);
        var isSuccess = respCode == PaymentResponseCodes.Success;

        if (!isSuccess && PaymentResponseCodes.IsNonFinal(respCode))
        {
            logger.LogInformation("Payment result for {InvoiceNo} is not final yet (respCode {RespCode}); left Pending",
                result.InvoiceNo, respCode);
            return;
        }

        var pending = db.Transactions.Where(t => t.Id == tx.Id && t.Status == TransactionStatus.Pending);
        int affected;
        if (isSuccess)
        {
            var channel = Truncate(result.ChannelCode, ChannelMaxLength);
            var masked = Truncate(result.AccountNo, MaskedCardMaxLength);
            var providerRef = Truncate(result.TranRef, ProviderRefMaxLength);
            // 2C2P transactionDateTime carries no timezone; record our own UTC clock instead.
            var paidAt = now;

            affected = await pending.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Succeeded)
                .SetProperty(t => t.PaymentChannel, channel)
                .SetProperty(t => t.MaskedCardNo, masked)
                .SetProperty(t => t.ProviderRef, providerRef)
                .SetProperty(t => t.PaidAt, paidAt)
                .SetProperty(t => t.RespCode, code)
                .SetProperty(t => t.RespDesc, desc)
                .SetProperty(t => t.UpdatedAt, now), ct);
        }
        else
        {
            affected = await pending.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Failed)
                .SetProperty(t => t.RespCode, code)
                .SetProperty(t => t.RespDesc, desc)
                .SetProperty(t => t.UpdatedAt, now), ct);
        }

        if (affected > 0)
        {
            logger.LogInformation("Transaction {InvoiceNo} marked {Status} (respCode {RespCode})",
                result.InvoiceNo, isSuccess ? TransactionStatus.Succeeded : TransactionStatus.Failed, code);
        }
        else
        {
            logger.LogInformation("Transaction {InvoiceNo} already final; duplicate result (respCode {RespCode}) ignored",
                result.InvoiceNo, code);
        }

        // Independent of `affected`: a notification retried after a crash between the update and the
        // card insert must still save the card exactly once.
        if (isSuccess && !string.IsNullOrWhiteSpace(result.CustomerToken))
        {
            await SaveCardAsync(tx.CustomerId, result.CustomerToken, result.AccountNo, now, ct);
        }
    }

    private async Task SaveCardAsync(int customerId, string customerToken, string? accountNo, DateTime now,
        CancellationToken ct)
    {
        var exists = await db.CustomerCards.AsNoTracking()
            .AnyAsync(c => c.CustomerId == customerId && c.CustomerToken == customerToken, ct);
        if (!exists)
        {
            var card = new CustomerCard
            {
                CustomerId = customerId,
                CustomerToken = customerToken,
                MaskedCardNo = Truncate(accountNo, MaskedCardMaxLength) ?? string.Empty,
                CreatedAt = now,
            };
            db.CustomerCards.Add(card);
            try
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Saved card {CardId} for customer {CustomerId}", card.Id, customerId);
            }
            catch (DbUpdateException)
            {
                // Unique (customer_id, customer_token) hit by a concurrent duplicate notification: the card exists.
                db.CustomerCards.Entry(card).State = EntityState.Detached;
                logger.LogDebug("Card for customer {CustomerId} already saved by a concurrent request", customerId);
            }
        }

        await cache.RemoveAsync(CacheKeys.CustomerCards(customerId), ct);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
