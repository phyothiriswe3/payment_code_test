using Checkout.Domain.Enums;

namespace Checkout.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "SGD";
    public PaymentType PaymentType { get; set; }
}
