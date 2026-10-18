using System.Net;
using System.Net.Http.Json;
using Checkout.Application.Dtos;
using Checkout.Application.Payments;
using Checkout.Domain.Entities;
using Checkout.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Checkout.IntegrationTests;

/// <summary>Plan T24: saved-card (customer token) path: DoPayment 2000 / 1001 (3DS) + inquiry.</summary>
[Collection(ApiCollection.Name)]
public sealed class SavedCardFlowTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CustomerId = 1;
    private const int ProductId = 3; // Monthly Membership, 99.00 SGD
    private const string ThreeDsUrl = "https://fake-2c2p.test/3ds/challenge";

    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();
        factory.Payments.TokenDelay = TimeSpan.Zero;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DoPayment_2000_with_successful_inquiry_succeeds_without_redirect()
    {
        var cardId = await SeedCardAsync();
        factory.Payments.OnDoPayment = _ => new DoPaymentResult("2000", "Completed", null);
        factory.Payments.OnInquire = Success;

        var response = await CheckoutAsync(cardId);

        response.Status.Should().Be("Succeeded");
        response.RedirectUrl.Should().BeNull();
        factory.Payments.DoPaymentCalls.Should().Be(1);
        factory.Payments.InquiryCalls.Should().Be(1);

        var tx = await _client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{response.InvoiceNo}");
        tx!.Status.Should().Be("Succeeded");
        tx.Flow.Should().Be("SavedCard");
        tx.Amount.Should().Be(99.00m);
        tx.MaskedCardNo.Should().Be("411111XXXXXX1111");
    }

    [Fact]
    public async Task DoPayment_1001_returns_3ds_redirect_then_confirm_succeeds()
    {
        var cardId = await SeedCardAsync();
        factory.Payments.OnDoPayment = req =>
        {
            req.CustomerToken.Should().Be("TOK-SAVED");
            return new DoPaymentResult("1001", "Redirect to 3DS", ThreeDsUrl);
        };

        var response = await CheckoutAsync(cardId);

        response.Status.Should().Be("Pending");
        response.RedirectUrl.Should().Be(ThreeDsUrl);
        factory.Payments.InquiryCalls.Should().Be(0);

        factory.Payments.OnInquire = Success;
        var confirm = await _client.PostAsync($"/api/transactions/{response.InvoiceNo}/confirm", null);
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        var tx = await confirm.Content.ReadFromJsonAsync<TransactionDto>();

        tx!.Status.Should().Be("Succeeded");
        tx.PaidAt.Should().NotBeNull();
        factory.Payments.InquiryCalls.Should().Be(1);
    }

    [Fact]
    public async Task DoPayment_non_final_5002_stays_pending_then_success_notification_settles()
    {
        var cardId = await SeedCardAsync();
        factory.Payments.OnDoPayment = _ => new DoPaymentResult("5002", "Timeout", null);

        var response = await CheckoutAsync(cardId);

        response.Status.Should().Be("Pending");
        var pending = await _client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{response.InvoiceNo}");
        pending!.Status.Should().Be("Pending");

        var jwt = ApiFactory.SignNotification(new Dictionary<string, object?>
        {
            ["merchantID"] = "JT01",
            ["invoiceNo"] = response.InvoiceNo,
            ["amount"] = 99.00m,
            ["currencyCode"] = "SGD",
            ["respCode"] = "0000",
            ["respDesc"] = "Success",
            ["channelCode"] = "CC",
            ["accountNo"] = "411111XXXXXX1111",
            ["tranRef"] = "REF-SC",
            ["customerToken"] = "TOK-SAVED",
            ["transactionDateTime"] = "20261017120000",
        });
        var notify = await _client.PostAsJsonAsync("/api/payments/2c2p/notification", new { payload = jwt });
        notify.StatusCode.Should().Be(HttpStatusCode.OK);

        var tx = await _client.GetFromJsonAsync<TransactionDto>($"/api/transactions/{response.InvoiceNo}");
        tx!.Status.Should().Be("Succeeded");
    }

    private static PaymentStatusResult Success(string invoiceNo) =>
        new(invoiceNo, 99.00m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "REF-SC", "TOK-SAVED", null);

    private async Task<CheckoutResponse> CheckoutAsync(int cardId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
        {
            Content = JsonContent.Create(new { customerId = CustomerId, productId = ProductId, customerCardId = cardId }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CheckoutResponse>())!;
    }

    private Task<int> SeedCardAsync() => factory.WithDbAsync(async db =>
    {
        var card = new CustomerCard
        {
            CustomerId = CustomerId, CustomerToken = "TOK-SAVED", MaskedCardNo = "411111XXXXXX1111", CreatedAt = DateTime.UtcNow,
        };
        db.CustomerCards.Add(card);
        await db.SaveChangesAsync();
        return card.Id;
    });
}
