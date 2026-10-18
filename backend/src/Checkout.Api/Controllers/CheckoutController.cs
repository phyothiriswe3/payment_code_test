using Swashbuckle.AspNetCore.Filters;
using System.ComponentModel.DataAnnotations;
using Checkout.Application;
using Checkout.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>Checkout request body.</summary>
/// <param name="CustomerId">Buying customer.</param>
/// <param name="ProductId">Product to buy.</param>
/// <param name="CustomerCardId">Saved card to charge; omit to use the 2C2P hosted payment page.</param>
public sealed record CheckoutRequest(
    [Range(1, int.MaxValue)] int CustomerId,
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, int.MaxValue)] int? CustomerCardId);

/// <summary>Starts payments.</summary>
[ApiController]
[Route("api/checkout")]
[Produces("application/json")]
public sealed class CheckoutController(ICheckoutService checkout) : ControllerBase
{
    /// <summary>
    /// Creates (or replays) a payment for the given customer and product. The same Idempotency-Key always
    /// returns the same transaction; reusing it with a different body returns 422.
    /// </summary>
    /// <param name="idempotencyKey">Client-generated unique key (UUID) for this purchase attempt.</param>
    /// <param name="request">Customer, product and optional saved card.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerRequestExample(typeof(CheckoutRequest), typeof(Checkout.Api.Swagger.CheckoutRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(Checkout.Api.Swagger.CheckoutResponseExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(Checkout.Api.Swagger.CheckoutBadRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(Checkout.Api.Swagger.CheckoutNotFoundExample))]
    [SwaggerResponseExample(StatusCodes.Status409Conflict, typeof(Checkout.Api.Swagger.CheckoutConflictExample))]
    [SwaggerResponseExample(StatusCodes.Status422UnprocessableEntity, typeof(Checkout.Api.Swagger.CheckoutUnprocessableExample))]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CheckoutRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            ModelState.AddModelError("Idempotency-Key", "The Idempotency-Key header is required.");
            return ValidationProblem(ModelState);
        }

        return await checkout.CheckoutAsync(
            new CheckoutCommand(idempotencyKey.Trim(), request.CustomerId, request.ProductId, request.CustomerCardId), ct);
    }
}
