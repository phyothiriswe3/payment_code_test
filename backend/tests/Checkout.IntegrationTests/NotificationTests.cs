using System.Net;
using System.Net.Http.Json;
using Checkout.Application.Dtos;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Checkout.Infrastructure.Persistence;
using Checkout.Infrastructure.Payments.TwoC2P;
using Checkout.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Checkout.IntegrationTests;

/// <summary>Plan T23: 2C2P backend notification handling (signature, idempotency, finality, card saving).</summary>
[Collection(ApiCollection.Name)]
public sealed class NotificationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string NotificationUrl = "/api/payments/2c2p/notification";
    private const int CustomerId = 1;
    private const int ProductId = 1; // Day Pass, 25.00 SGD
    private const decimal Amount = 25.00m;

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Valid_success_notification_marks_succeeded_and_saves_fields_and_card()
    {
        var invoice = await SeedPendingAsync();

        var response = await PostAsync(ApiFactory.SignNotification(Payload(invoice, "0000", token: "TOK-A")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tx = await LoadTxAsync(invoice);
        tx.Status.Should().Be(TransactionStatus.Succeeded);
        tx.RespCode.Should().Be("0000");
        tx.PaymentChannel.Should().Be("CC");
        tx.MaskedCardNo.Should().Be("411111XXXXXX1111");
        tx.ProviderRef.Should().Be("REF-1");
        tx.PaidAt.Should().NotBeNull();
        (await CardsAsync()).Should().ContainSingle(c => c.CustomerToken == "TOK-A");
    }

    [Fact]
    public async Task Same_notification_twice_keeps_one_card_and_status()
    {
        var invoice = await SeedPendingAsync();
        var jwt = ApiFactory.SignNotification(Payload(invoice, "0000", token: "TOK-A"));

        (await PostAsync(jwt)).StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await LoadTxAsync(invoice);
        (await PostAsync(jwt)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await LoadTxAsync(invoice);

        second.Status.Should().Be(TransactionStatus.Succeeded);
        second.PaidAt.Should().Be(first.PaidAt);
        second.UpdatedAt.Should().Be(first.UpdatedAt);
        (await CardsAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Five_parallel_identical_notifications_save_one_card()
    {
        var invoice = await SeedPendingAsync();
        var jwt = ApiFactory.SignNotification(Payload(invoice, "0000", token: "TOK-PAR"));

        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await gate.Task;
            using var client = factory.CreateClient();
            return await client.PostAsJsonAsync(NotificationUrl, new { payload = jwt });
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await LoadTxAsync(invoice)).Status.Should().Be(TransactionStatus.Succeeded);
        (await CardsAsync()).Should().ContainSingle(c => c.CustomerToken == "TOK-PAR");
    }

    [Fact]
    public async Task Bad_signature_returns_400_and_changes_nothing()
    {
        var invoice = await SeedPendingAsync();
        var forged = new TwoC2PJwt("some-other-secret-that-is-not-the-merchant-key")
            .Sign(Payload(invoice, "0000", token: "TOK-X"));

        var response = await PostAsync(forged);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoadTxAsync(invoice)).Status.Should().Be(TransactionStatus.Pending);
        (await CardsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_payload_returns_400()
    {
        var response = await _client.PostAsJsonAsync(NotificationUrl, new { payload = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Amount_mismatch_is_ignored()
    {
        var invoice = await SeedPendingAsync();

        var response = await PostAsync(ApiFactory.SignNotification(Payload(invoice, "0000", amount: 1.00m, token: "TOK-M")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadTxAsync(invoice)).Status.Should().Be(TransactionStatus.Pending);
        (await CardsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_result_is_final_and_later_success_is_ignored()
    {
        var invoice = await SeedPendingAsync();

        (await PostAsync(ApiFactory.SignNotification(Payload(invoice, "4200", desc: "Declined")))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await LoadTxAsync(invoice)).Status.Should().Be(TransactionStatus.Failed);

        (await PostAsync(ApiFactory.SignNotification(Payload(invoice, "0000")))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var tx = await LoadTxAsync(invoice);
        tx.Status.Should().Be(TransactionStatus.Failed);
        tx.RespCode.Should().Be("4200");
        tx.PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task Non_final_code_leaves_transaction_pending()
    {
        var invoice = await SeedPendingAsync();

        (await PostAsync(ApiFactory.SignNotification(Payload(invoice, "0001")))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await LoadTxAsync(invoice)).Status.Should().Be(TransactionStatus.Pending);
    }

    [Fact]
    public async Task Cards_endpoint_shows_new_card_after_notification()
    {
        // Prime the cache with the empty list.
        (await _client.GetFromJsonAsync<List<CardDto>>($"/api/customers/{CustomerId}/cards"))
            .Should().BeEmpty();

        var invoice = await SeedPendingAsync();
        (await PostAsync(ApiFactory.SignNotification(Payload(invoice, "0000", token: "TOK-C")))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var cards = await _client.GetFromJsonAsync<List<CardDto>>($"/api/customers/{CustomerId}/cards");
        cards.Should().ContainSingle().Which.MaskedCardNo.Should().Be("411111XXXXXX1111");
    }

    private Task<HttpResponseMessage> PostAsync(string jwt) =>
        _client.PostAsJsonAsync(NotificationUrl, new { payload = jwt });

    private static object Payload(string invoice, string respCode, decimal amount = Amount, string? token = null,
        string desc = "Success") => new Dictionary<string, object?>
    {
        ["merchantID"] = "JT01",
        ["invoiceNo"] = invoice,
        ["amount"] = amount,
        ["currencyCode"] = "SGD",
        ["respCode"] = respCode,
        ["respDesc"] = desc,
        ["channelCode"] = "CC",
        ["accountNo"] = "411111XXXXXX1111",
        ["tranRef"] = "REF-1",
        ["customerToken"] = token,
        ["transactionDateTime"] = "20261017120000",
    };

    private async Task<string> SeedPendingAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
        var now = DateTime.UtcNow;
        var tx = new PaymentTransaction
        {
            CustomerId = CustomerId,
            ProductId = ProductId,
            InvoiceNo = "T" + Guid.NewGuid().ToString("N")[..18],
            IdempotencyKey = Guid.NewGuid().ToString("D"),
            RequestFingerprint = "test",
            Amount = Amount,
            Currency = "SGD",
            Status = TransactionStatus.Pending,
            Flow = PaymentFlow.HostedPage,
            CreatedAt = now,
            UpdatedAt = now,
            ProcessedAt = now,
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.InvoiceNo;
    }

    private async Task<PaymentTransaction> LoadTxAsync(string invoice)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
        return await db.Transactions.AsNoTracking().SingleAsync(t => t.InvoiceNo == invoice);
    }

    private async Task<List<CustomerCard>> CardsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
        return await db.CustomerCards.AsNoTracking().Where(c => c.CustomerId == CustomerId).ToListAsync();
    }
}
