using System.Net;
using System.Net.Http.Json;
using Checkout.Application.Dtos;
using Checkout.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Checkout.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class CheckoutConcurrencyTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly object Body = new { customerId = 1, productId = 1 };

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Parallel_requests_with_one_key_create_one_transaction_and_one_provider_call()
    {
        var key = Guid.NewGuid().ToString();

        var results = await FireInParallelAsync(factory, key, Body, 20);

        await AssertSingleChargeAsync(key, results);
    }

    [Fact]
    public async Task Sequential_replay_returns_same_invoice_without_calling_provider_again()
    {
        var key = Guid.NewGuid().ToString();
        var client = factory.CreateClient();

        var first = await PostAsync(client, key, Body);
        var second = await PostAsync(client, key, Body);

        first.Status.Should().Be(HttpStatusCode.OK);
        second.Status.Should().Be(HttpStatusCode.OK);
        second.Body!.InvoiceNo.Should().Be(first.Body!.InvoiceNo);
        second.Body.RedirectUrl.Should().Be(first.Body.RedirectUrl);
        factory.Payments.TokenCalls.Should().Be(1);
        (await CountRowsAsync(key)).Should().Be(1);
    }

    [Fact]
    public async Task Same_key_with_different_body_returns_422()
    {
        var key = Guid.NewGuid().ToString();
        var client = factory.CreateClient();

        (await PostAsync(client, key, Body)).Status.Should().Be(HttpStatusCode.OK);
        var mismatch = await PostAsync(client, key, new { customerId = 1, productId = 2 });

        mismatch.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        factory.Payments.TokenCalls.Should().Be(1);
        (await CountRowsAsync(key)).Should().Be(1);
    }

    [Fact]
    public async Task Without_the_lock_the_unique_index_still_allows_one_transaction()
    {
        var key = Guid.NewGuid().ToString();
        await using var noLock = factory.WithoutDistributedLock();

        var results = await FireInParallelAsync(noLock, key, Body, 10);

        await AssertSingleChargeAsync(key, results);
    }

    private async Task AssertSingleChargeAsync(string key, IReadOnlyList<(HttpStatusCode Status, CheckoutResponse? Body)> results)
    {
        (await CountRowsAsync(key)).Should().Be(1);
        factory.Payments.TokenCalls.Should().Be(1);

        results.Should().OnlyContain(r => r.Status == HttpStatusCode.OK || r.Status == HttpStatusCode.Conflict);
        var ok = results.Where(r => r.Status == HttpStatusCode.OK).Select(r => r.Body!).ToList();
        ok.Should().NotBeEmpty();
        ok.Select(r => r.InvoiceNo).Distinct().Should().ContainSingle();
        ok.Select(r => r.RedirectUrl).Distinct().Should().ContainSingle()
            .Which.Should().Be(FakePaymentProvider.FakeWebPaymentUrl(ok[0].InvoiceNo));
    }

    private static async Task<IReadOnlyList<(HttpStatusCode Status, CheckoutResponse? Body)>> FireInParallelAsync(
        WebApplicationFactory<Program> app, string key, object body, int count)
    {
        var clients = Enumerable.Range(0, count).Select(_ => app.CreateClient()).ToList();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = clients.Select(async c =>
        {
            await gate.Task;
            return await PostAsync(c, key, body);
        }).ToList();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private static async Task<(HttpStatusCode Status, CheckoutResponse? Body)> PostAsync(HttpClient client, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        var parsed = response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<CheckoutResponse>() : null;
        return (response.StatusCode, parsed);
    }

    private Task<int> CountRowsAsync(string key) =>
        factory.WithDbAsync(db => db.Transactions.CountAsync(t => t.IdempotencyKey == key));
}
