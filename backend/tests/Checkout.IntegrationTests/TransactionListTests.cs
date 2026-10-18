using System.Net;
using System.Net.Http.Json;
using Checkout.Application.Dtos;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Checkout.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Checkout.IntegrationTests;

/// <summary>GET /api/transactions: server-side pagination, ordering, status filter, validation.</summary>
[Collection(ApiCollection.Name)]
public sealed class TransactionListTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Url = "/api/transactions";
    private static readonly DateTime BaseTime = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 12 rows: i = 0..11, CreatedAt = BaseTime + i hours, except rows 10 and 11 share row 9's CreatedAt
    /// (ties broken by Id desc). Status cycles Pending/Succeeded/Failed. Returns invoice numbers in expected order.
    /// </summary>
    private async Task<List<string>> SeedAsync()
    {
        var rows = Enumerable.Range(0, 12).Select(i =>
        {
            var created = BaseTime.AddHours(Math.Min(i, 9));
            return new PaymentTransaction
            {
                CustomerId = i % 2 == 0 ? 1 : 2,
                ProductId = i % 2 == 0 ? 1 : 3,
                InvoiceNo = $"TLIST{i:D2}",
                IdempotencyKey = Guid.NewGuid().ToString("D"),
                RequestFingerprint = "test",
                Amount = i % 2 == 0 ? 25.00m : 99.00m,
                Currency = "SGD",
                Status = (TransactionStatus)(i % 3),
                Flow = PaymentFlow.HostedPage,
                CreatedAt = created,
                UpdatedAt = created,
                ProcessedAt = created,
            };
        }).ToList();

        // Insert in shuffled order so ordering cannot come from insertion order alone (except the Id tie-break).
        await factory.WithDbAsync(async db =>
        {
            foreach (var tx in rows.OrderBy(r => r.InvoiceNo == "TLIST05" ? 0 : 1))
            {
                db.Transactions.Add(tx);
                await db.SaveChangesAsync();
            }
            return 0;
        });

        return rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Select(r => r.InvoiceNo).ToList();
    }

    [Fact]
    public async Task First_page_is_newest_first_with_counts()
    {
        var expected = await SeedAsync();

        var page = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>($"{Url}?page=1&pageSize=5");

        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(5);
        page.TotalCount.Should().Be(12);
        page.TotalPages.Should().Be(3);
        page.Items.Select(t => t.InvoiceNo).Should().Equal(expected.Take(5));
        // Ties on CreatedAt (rows 9..11) resolve by Id desc: inserted 11 last => highest id first.
        page.Items.Select(t => t.InvoiceNo).Take(3).Should().Equal("TLIST11", "TLIST10", "TLIST09");
    }

    [Fact]
    public async Task Second_and_last_pages_return_the_right_slices()
    {
        var expected = await SeedAsync();

        var second = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>($"{Url}?page=2&pageSize=5");
        var last = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>($"{Url}?page=3&pageSize=5");
        var beyond = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>($"{Url}?page=9&pageSize=5");

        second!.Items.Select(t => t.InvoiceNo).Should().Equal(expected.Skip(5).Take(5));
        last!.Items.Select(t => t.InvoiceNo).Should().Equal(expected.Skip(10));
        beyond!.Items.Should().BeEmpty();
        beyond.TotalCount.Should().Be(12);
    }

    [Fact]
    public async Task Defaults_are_page_1_and_size_10()
    {
        await SeedAsync();

        var page = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>(Url);

        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(10);
        page.Items.Should().HaveCount(10);
        page.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Status_filter_limits_items_and_counts()
    {
        var expected = await SeedAsync();

        var page = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>($"{Url}?status=Succeeded&pageSize=3");

        // Succeeded = i % 3 == 1 => rows 1, 4, 7, 10.
        page!.TotalCount.Should().Be(4);
        page.TotalPages.Should().Be(2);
        page.Items.Should().OnlyContain(t => t.Status == "Succeeded");
        page.Items.Select(t => t.InvoiceNo).Should()
            .Equal(expected.Where(i => new[] { "TLIST01", "TLIST04", "TLIST07", "TLIST10" }.Contains(i)).Take(3));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("status=Bogus")]
    [InlineData("status=1")]
    public async Task Invalid_query_returns_400_problem_details(string query)
    {
        var response = await _client.GetAsync($"{Url}?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(400);
    }

    [Fact]
    public async Task Items_and_get_by_invoice_include_customer_and_payment_type()
    {
        await SeedAsync();

        var tx = await _client.GetFromJsonAsync<TransactionDto>($"{Url}/TLIST01");
        tx!.CustomerName.Should().Be("Mei Ling Tan");
        tx.CustomerEmail.Should().Be("meiling.tan@example.com");
        tx.PaymentType.Should().Be("Recurring");
        tx.ProductName.Should().Be("Monthly Membership");

        var page = await _client.GetFromJsonAsync<PagedResult<TransactionDto>>($"{Url}?pageSize=100");
        var day = page!.Items.Single(t => t.InvoiceNo == "TLIST00");
        day.CustomerName.Should().Be("Aung Min");
        day.CustomerEmail.Should().Be("aung.min@example.com");
        day.PaymentType.Should().Be("OneTime");
    }

    [Fact]
    public async Task Json_uses_contract_property_names()
    {
        await SeedAsync();

        using var doc = System.Text.Json.JsonDocument.Parse(await _client.GetStringAsync($"{Url}?pageSize=1"));
        var root = doc.RootElement;
        root.GetProperty("page").GetInt32().Should().Be(1);
        root.GetProperty("pageSize").GetInt32().Should().Be(1);
        root.GetProperty("totalCount").GetInt32().Should().Be(12);
        root.GetProperty("totalPages").GetInt32().Should().Be(12);
        var item = root.GetProperty("items")[0];
        item.GetProperty("customerName").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("customerEmail").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("paymentType").GetString().Should().BeOneOf("OneTime", "Recurring");
        // Timestamps are UTC and must say so, otherwise browsers parse them as local time.
        item.GetProperty("createdAt").GetString().Should().EndWith("Z");
    }
}
