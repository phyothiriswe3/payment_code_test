using Swashbuckle.AspNetCore.Filters;
using Checkout.Application;
using Checkout.Application.Dtos;
using Checkout.Application.Errors;
using Checkout.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>Transaction status.</summary>
[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
public sealed class TransactionsController(ITransactionQueryService query, IPaymentResultService results) : ControllerBase
{
    public const int MaxPageSize = 100;

    /// <summary>Lists transactions newest first (CreatedAt desc, then Id desc), read fresh from the database.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, 1-100 (default 10).</param>
    /// <param name="status">Optional filter: Pending, Succeeded or Failed.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<TransactionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(Checkout.Api.Swagger.TransactionPageExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(Checkout.Api.Swagger.TransactionListBadRequestExample))]
    public async Task<PagedResult<TransactionDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        if (page < 1)
            throw new ValidationException("page must be 1 or greater.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");

        return await query.ListAsync(page, pageSize, ParseStatus(status), ct);
    }

    private static TransactionStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        var name = Enum.GetNames<TransactionStatus>()
            .FirstOrDefault(n => string.Equals(n, status, StringComparison.OrdinalIgnoreCase));
        return name is null
            ? throw new ValidationException(
                $"status must be one of: {string.Join(", ", Enum.GetNames<TransactionStatus>())}.")
            : Enum.Parse<TransactionStatus>(name);
    }

    /// <summary>Returns the transaction, read fresh from the database.</summary>
    /// <param name="invoiceNo">Invoice number returned by checkout.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{invoiceNo}")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(Checkout.Api.Swagger.TransactionExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(Checkout.Api.Swagger.TransactionNotFoundExample))]
    public async Task<TransactionDto> Get(string invoiceNo, CancellationToken ct) =>
        await query.GetAsync(invoiceNo, ct) ?? throw new NotFoundException($"Transaction '{invoiceNo}' was not found.");

    /// <summary>Runs a 2C2P Payment Inquiry if the transaction is still Pending, then returns it.</summary>
    /// <param name="invoiceNo">Invoice number returned by checkout.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{invoiceNo}/confirm")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(Checkout.Api.Swagger.TransactionExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(Checkout.Api.Swagger.TransactionNotFoundExample))]
    public async Task<TransactionDto> Confirm(string invoiceNo, CancellationToken ct) =>
        await results.ConfirmAsync(invoiceNo, ct);
}
