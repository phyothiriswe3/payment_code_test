using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Product> Products { get; }
    DbSet<CustomerCard> CustomerCards { get; }
    DbSet<PaymentTransaction> Transactions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
