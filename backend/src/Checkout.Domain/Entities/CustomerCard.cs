namespace Checkout.Domain.Entities;

public class CustomerCard
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerToken { get; set; } = string.Empty;
    public string MaskedCardNo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
