using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Checkout.Application.Abstractions;
using Checkout.Application.Dtos;
using Checkout.Application.Errors;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Checkout.Application.Checkout;

/// <summary>
/// Idempotent checkout. Exactly one request per Idempotency-Key reaches 2C2P; every other request with the
/// same key replays the settled result, waits for it, or gets 409.
/// Guards, outermost first:
///   1. Fast path: a settled row for the key is replayed without touching Redis or 2C2P.
///   2. Redis lock <c>lock:checkout:{key}</c>: concurrent duplicates wait instead of racing.
///   3. MySQL unique index on <c>idempotency_key</c>: the final guard when the lock expired or Redis is down.
/// </summary>
public sealed class CheckoutService(
    IAppDbContext db,
    IDistributedLock lockSvc,
    IHostedPaymentFlow hosted,
    ISavedCardPaymentFlow savedCard,
    IInvoiceNumberGenerator invoices,
    IClock clock,
    IOptions<CheckoutOptions> options,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    private const string InProgressMessage = "Checkout with this Idempotency-Key is in progress.";

    private readonly CheckoutOptions _options = options.Value;

    public async Task<CheckoutResponse> CheckoutAsync(CheckoutCommand command, CancellationToken ct)
    {
        // Step 1: validate the request before anything is locked or written.
        var key = NormalizeIdempotencyKey(command.IdempotencyKey);
        var (product, card) = await LoadAndValidateAsync(command, ct);

        // Step 2: fingerprint the body so a reused key with a different body is rejected (422).
        var fingerprint = ComputeFingerprint(command);

        // Step 3: fast path. Replays a settled result and never calls 2C2P twice.
        var existing = await FindByKeyAsync(key, ct);
        if (existing is not null)
            return await ReplayAsync(existing, fingerprint, ct);

        // Step 4: serialise requests for this key.
        ILockHandle? handle;
        try
        {
            handle = await lockSvc.TryAcquireAsync($"checkout:{key}", TimeSpan.FromSeconds(_options.CheckoutLockTtlSeconds), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Redis is down: carry on without the lock. The unique index on idempotency_key still allows only one
            // insert, and the loser of that race waits for the winner's result (CreateAndPayAsync).
            logger.LogWarning(ex, "Checkout lock unavailable for key {IdempotencyKey}; relying on the database unique index", key);
            return await CreateAndPayAsync(command, key, fingerprint, product, card, ct);
        }

        // Another request holds the lock: wait for its result instead of racing it.
        if (handle is null)
            return ToResponse(await WaitForSettledAsync(key, fingerprint, ct));

        await using (handle)
        {
            // Step 5: double-check now that we own the lock; the previous owner may have finished just before.
            existing = await FindByKeyAsync(key, ct);
            if (existing is not null)
            {
                EnsureSameRequest(existing, fingerprint);

                // An unsettled row while we hold the lock means its owner crashed or its lock expired mid-call.
                // Answer 409 instead of calling 2C2P again: never risk a second charge.
                if (!IsSettled(existing))
                    throw new ConflictException(InProgressMessage);

                return ToResponse(existing);
            }

            // Steps 6 and 7.
            return await CreateAndPayAsync(command, key, fingerprint, product, card, ct);
        }
    }

    private async Task<(Product Product, CustomerCard? Card)> LoadAndValidateAsync(CheckoutCommand command, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == command.CustomerId, ct))
            throw new NotFoundException($"Customer {command.CustomerId} was not found.");

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == command.ProductId, ct)
            ?? throw new NotFoundException($"Product {command.ProductId} was not found.");

        if (command.CustomerCardId is not { } cardId)
            return (product, null);

        // Filter by owner in the same query: a client can only pay with its own customer's saved cards,
        // and another customer's card id looks the same as an unknown one.
        var card = await db.CustomerCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.CustomerId == command.CustomerId, ct)
            ?? throw new ValidationException($"Card {cardId} does not belong to customer {command.CustomerId}.");

        return (product, card);
    }

    // Steps 6 and 7: insert the Pending row (the claim on the key), then run the payment flow.
    private async Task<CheckoutResponse> CreateAndPayAsync(
        CheckoutCommand command, string key, string fingerprint, Product product, CustomerCard? card, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tx = new PaymentTransaction
        {
            CustomerId = command.CustomerId,
            ProductId = product.Id,
            CustomerCardId = card?.Id,
            InvoiceNo = invoices.Next(),
            IdempotencyKey = key,
            RequestFingerprint = fingerprint,
            // Price and currency always come from the product, never from the client.
            Amount = product.Price,
            Currency = product.Currency,
            Status = TransactionStatus.Pending,
            Flow = card is not null ? PaymentFlow.SavedCard : PaymentFlow.HostedPage,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Transactions.Add(tx);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Stop tracking the failed insert so a later SaveChanges on this context doesn't retry it.
            db.Transactions.Entry(tx).State = EntityState.Detached;

            // A row with our key now exists => another request won the unique-index race (lock expired or
            // Redis down). Anything else (e.g. an invoice number collision) is a genuine failure.
            var winner = await FindByKeyAsync(key, ct);
            if (winner is null)
                throw;

            logger.LogInformation("Checkout for key {IdempotencyKey} lost the insert race to invoice {InvoiceNo}", key, winner.InvoiceNo);
            return await ReplayAsync(winner, fingerprint, ct);
        }

        logger.LogInformation("Created {Flow} transaction {InvoiceNo} for key {IdempotencyKey}", tx.Flow, tx.InvoiceNo, key);

        // Step 7: the flow calls 2C2P and sets ProcessedAt, which releases any waiting duplicates.
        return card is not null
            ? await savedCard.ExecuteAsync(tx, product, card, ct)
            : await hosted.ExecuteAsync(tx, product, ct);
    }

    // Returns a settled row as is. Never returns an unsettled one: a Pending row without a redirect URL would send
    // a hosted-page client straight to the result page. Waits for it to settle instead.
    private async Task<CheckoutResponse> ReplayAsync(PaymentTransaction existing, string fingerprint, CancellationToken ct)
    {
        EnsureSameRequest(existing, fingerprint);
        return IsSettled(existing)
            ? ToResponse(existing)
            : ToResponse(await WaitForSettledAsync(existing.IdempotencyKey, fingerprint, ct));
    }

    // Polls MySQL until the row for the key is settled, or throws 409 after CheckoutWaitSeconds.
    private async Task<PaymentTransaction> WaitForSettledAsync(string key, string fingerprint, CancellationToken ct)
    {
        var maxWait = TimeSpan.FromSeconds(_options.CheckoutWaitSeconds);
        var pollInterval = TimeSpan.FromMilliseconds(_options.WaitPollMilliseconds);
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            var tx = await FindByKeyAsync(key, ct);
            if (tx is not null)
            {
                EnsureSameRequest(tx, fingerprint);
                if (IsSettled(tx))
                    return tx;
            }

            if (Stopwatch.GetElapsedTime(started) >= maxWait)
                throw new ConflictException(InProgressMessage);

            await Task.Delay(pollInterval, ct);
        }
    }

    // AsNoTracking: every poll must read the current row, not an instance cached by the change tracker.
    private Task<PaymentTransaction?> FindByKeyAsync(string key, CancellationToken ct) =>
        db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    // Settled = the call to 2C2P has finished, so replaying the stored result is safe.
    private static bool IsSettled(PaymentTransaction tx) => tx.ProcessedAt is not null;

    private static void EnsureSameRequest(PaymentTransaction tx, string fingerprint)
    {
        if (!string.Equals(tx.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new IdempotencyMismatchException("Idempotency-Key was already used with a different request body.");
    }

    // Requires a UUID and stores it in canonical lowercase form, so differently formatted copies of the same key
    // map to one Redis lock and one database row.
    private static string NormalizeIdempotencyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ValidationException("Idempotency-Key header is required.");
        if (!Guid.TryParse(key, out var guid))
            throw new ValidationException("Idempotency-Key must be a UUID.");
        return guid.ToString("D");
    }

    // SHA-256 hex of "customerId|productId|cardId" (cardId empty for the hosted page).
    private static string ComputeFingerprint(CheckoutCommand command)
    {
        var body = string.Create(CultureInfo.InvariantCulture, $"{command.CustomerId}|{command.ProductId}|{command.CustomerCardId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private static CheckoutResponse ToResponse(PaymentTransaction tx) =>
        new(tx.InvoiceNo, tx.Status.ToString(), tx.RedirectUrl);
}
