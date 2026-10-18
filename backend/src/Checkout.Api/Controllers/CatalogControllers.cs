using Checkout.Application;
using Checkout.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>Customers and their saved cards.</summary>
[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Lists all customers.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CustomerDto>> GetCustomers(CancellationToken ct) =>
        await catalog.GetCustomersAsync(ct);

    /// <summary>Lists the customer's saved (tokenized) cards. Empty when none are saved.</summary>
    /// <param name="id">Customer id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:int}/cards")]
    [ProducesResponseType<IReadOnlyList<CardDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<CardDto>> GetCards(int id, CancellationToken ct) =>
        await catalog.GetCardsAsync(id, ct);
}

/// <summary>Product catalog.</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public sealed class ProductsController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Lists all products.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProductDto>> GetProducts(CancellationToken ct) =>
        await catalog.GetProductsAsync(ct);
}
