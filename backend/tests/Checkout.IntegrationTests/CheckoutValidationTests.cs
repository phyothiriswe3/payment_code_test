using System.Net;
using System.Net.Http.Json;
using Checkout.Domain.Entities;
using Checkout.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Checkout.IntegrationTests;

/// <summary>Plan T24: checkout request validation (nothing reaches 2C2P, nothing is written).</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckoutValidationTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/checkout", new { customerId = 1, productId = 1 });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_uuid_idempotency_key_returns_400()
    {
        var response = await PostAsync(new { customerId = 1, productId = 1 }, key: "not-a-uuid");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_customer_returns_404()
    {
        var response = await PostAsync(new { customerId = 999, productId = 1 });

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        var response = await PostAsync(new { customerId = 1, productId = 999 });

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Card_of_another_customer_returns_400()
    {
        var otherCardId = await factory.WithDbAsync(async db =>
        {
            var card = new CustomerCard
            {
                CustomerId = 2, CustomerToken = "TOK-OTHER", MaskedCardNo = "411111XXXXXX1111", CreatedAt = DateTime.UtcNow,
            };
            db.CustomerCards.Add(card);
            await db.SaveChangesAsync();
            return card.Id;
        });

        var response = await PostAsync(new { customerId = 1, productId = 3, customerCardId = otherCardId });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_card_returns_400()
    {
        var response = await PostAsync(new { customerId = 1, productId = 3, customerCardId = 12345 });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    private Task<HttpResponseMessage> PostAsync(object body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return _client.SendAsync(request);
    }

    private async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        // [Produces("application/json")] on the controller makes the header-validation ValidationProblem plain JSON.
        response.Content.Headers.ContentType?.MediaType.Should().BeOneOf("application/problem+json", "application/json");
        problem!.Status.Should().Be((int)expected);

        factory.Payments.TokenCalls.Should().Be(0);
        (await factory.WithDbAsync(db => db.Transactions.CountAsync())).Should().Be(0);
    }
}
