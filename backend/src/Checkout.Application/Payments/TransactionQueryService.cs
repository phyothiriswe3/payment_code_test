using System.Linq.Expressions;
using Checkout.Application.Abstractions;
using Checkout.Application.Dtos;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Application.Payments;

public sealed class TransactionQueryService(IAppDbContext db) : ITransactionQueryService
{
    public async Task<TransactionDto?> GetAsync(string invoiceNo, CancellationToken ct)
    {
        var row = await db.Transactions.AsNoTracking()
            .Where(t => t.InvoiceNo == invoiceNo)
            .Select(ToRow)
            .FirstOrDefaultAsync(ct);

        return row?.ToDto();
    }

    public async Task<PagedResult<TransactionDto>> ListAsync(int page, int pageSize, TransactionStatus? status,
        CancellationToken ct)
    {
        var query = db.Transactions.AsNoTracking();
        if (status is { } s)
            query = query.Where(t => t.Status == s);

        var totalCount = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToRow)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<TransactionDto>(rows.Select(r => r.ToDto()).ToList(), page, pageSize, totalCount,
            totalPages);
    }

    private static readonly Expression<Func<PaymentTransaction, Row>> ToRow = t => new Row
    {
        InvoiceNo = t.InvoiceNo,
        Status = t.Status,
        Amount = t.Amount,
        Currency = t.Currency,
        CustomerId = t.CustomerId,
        ProductId = t.ProductId,
        ProductName = t.Product!.Name,
        Flow = t.Flow,
        PaymentChannel = t.PaymentChannel,
        MaskedCardNo = t.MaskedCardNo,
        RespCode = t.RespCode,
        RespDesc = t.RespDesc,
        CreatedAt = t.CreatedAt,
        PaidAt = t.PaidAt,
        CustomerFirstName = t.Customer!.FirstName,
        CustomerLastName = t.Customer!.LastName,
        CustomerEmail = t.Customer!.Email,
        PaymentType = t.Product!.PaymentType,
    };

    private sealed class Row
    {
        public required string InvoiceNo { get; init; }
        public TransactionStatus Status { get; init; }
        public decimal Amount { get; init; }
        public required string Currency { get; init; }
        public int CustomerId { get; init; }
        public int ProductId { get; init; }
        public required string ProductName { get; init; }
        public PaymentFlow Flow { get; init; }
        public string? PaymentChannel { get; init; }
        public string? MaskedCardNo { get; init; }
        public string? RespCode { get; init; }
        public string? RespDesc { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? PaidAt { get; init; }
        public required string CustomerFirstName { get; init; }
        public required string CustomerLastName { get; init; }
        public required string CustomerEmail { get; init; }
        public PaymentType PaymentType { get; init; }

        public TransactionDto ToDto() => new(InvoiceNo, Status.ToString(), Amount, Currency, CustomerId, ProductId,
            ProductName, Flow.ToString(), PaymentChannel, MaskedCardNo, RespCode, RespDesc, CreatedAt, PaidAt,
            $"{CustomerFirstName} {CustomerLastName}", CustomerEmail, PaymentType.ToString());
    }
}
