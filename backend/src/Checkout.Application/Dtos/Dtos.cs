namespace Checkout.Application.Dtos;

public record CheckoutCommand(string IdempotencyKey, int CustomerId, int ProductId, int? CustomerCardId);
public record CheckoutResponse(string InvoiceNo, string Status, string? RedirectUrl);
public record CustomerDto(int Id, string FirstName, string LastName, string Email);
public record ProductDto(int Id, string Name, string? Description, decimal Price, string Currency, string PaymentType);
public record CardDto(int Id, string MaskedCardNo);
public record TransactionDto(
    string InvoiceNo, string Status, decimal Amount, string Currency, int CustomerId, int ProductId,
    string ProductName, string Flow, string? PaymentChannel, string? MaskedCardNo, string? RespCode, string? RespDesc,
    DateTime CreatedAt, DateTime? PaidAt, string CustomerName, string CustomerEmail, string PaymentType);
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
