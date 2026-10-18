using Checkout.Application.Abstractions;
using Checkout.Application.Dtos;
using Checkout.Application.Errors;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Application.Catalog;

public sealed class CatalogService(IAppDbContext db, ICacheService cache) : ICatalogService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken ct) =>
        cache.GetOrSetAsync<IReadOnlyList<CustomerDto>>(CacheKeys.Customers, async c =>
            await db.Customers.AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new CustomerDto(x.Id, x.FirstName, x.LastName, x.Email))
                .ToListAsync(c), Ttl, ct);

    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct) =>
        cache.GetOrSetAsync<IReadOnlyList<ProductDto>>(CacheKeys.Products, async c =>
        {
            var rows = await db.Products.AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name, x.Description, x.Price, x.Currency, x.PaymentType })
                .ToListAsync(c);
            return rows.Select(x => new ProductDto(x.Id, x.Name, x.Description, x.Price, x.Currency, x.PaymentType.ToString())).ToList();
        }, Ttl, ct);

    public async Task<IReadOnlyList<CardDto>> GetCardsAsync(int customerId, CancellationToken ct)
    {
        if (!await db.Customers.AsNoTracking().AnyAsync(x => x.Id == customerId, ct))
            throw new NotFoundException($"Customer {customerId} not found.");

        return await cache.GetOrSetAsync<IReadOnlyList<CardDto>>(CacheKeys.CustomerCards(customerId), async c =>
            await db.CustomerCards.AsNoTracking()
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CardDto(x.Id, x.MaskedCardNo))
                .ToListAsync(c), Ttl, ct);
    }
}
