namespace Checkout.Application.Catalog;

public static class CacheKeys
{
    public const string Customers = "cache:customers";
    public const string Products = "cache:products";
    public static string CustomerCards(int customerId) => $"cache:customer:{customerId}:cards";
}
