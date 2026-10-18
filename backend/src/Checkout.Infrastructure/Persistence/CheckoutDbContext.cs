using Checkout.Application.Abstractions;
using Checkout.Domain.Entities;
using Checkout.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Checkout.Infrastructure.Persistence;

public sealed class CheckoutDbContext(DbContextOptions<CheckoutDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CustomerCard> CustomerCards => Set<CustomerCard>();
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();

    /// <summary>All DATETIME columns hold UTC; mark values read back as Utc so the API serialises them with "Z".</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcConverter>();
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CheckoutDbContext).Assembly);
        modelBuilder.ApplySeed();
    }
}
