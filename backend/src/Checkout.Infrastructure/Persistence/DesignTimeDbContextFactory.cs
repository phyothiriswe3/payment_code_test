using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Checkout.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef`; a fixed server version means no live MySQL is needed to add migrations.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CheckoutDbContext>
{
    private const string DesignTimeConnectionString = "server=localhost;port=3306;database=checkout;user=checkout;password=design-time";

    public CheckoutDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CheckoutDbContext>()
            .UseMySql(DesignTimeConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        return new CheckoutDbContext(options);
    }
}
