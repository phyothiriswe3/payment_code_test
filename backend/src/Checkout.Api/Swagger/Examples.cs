using Checkout.Api.Controllers;
using Checkout.Application.Dtos;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;

namespace Checkout.Api.Swagger;

public sealed class CustomersExample : IExamplesProvider<IReadOnlyList<CustomerDto>>
{
    public IReadOnlyList<CustomerDto> GetExamples() =>
    [
        new(1, "Aung", "Min", "aung.min@example.com"),
        new(2, "Su", "Su", "su.su@example.com"),
    ];
}

public sealed class ProductsExample : IExamplesProvider<IReadOnlyList<ProductDto>>
{
    public IReadOnlyList<ProductDto> GetExamples() =>
    [
        new(1, "Yoga Class Pass", "Single drop-in class", 25.00m, "SGD", "OneTime"),
        new(3, "Monthly Membership", "Unlimited classes, billed monthly", 99.00m, "SGD", "Recurring"),
    ];
}

public sealed class CardsExample : IExamplesProvider<IReadOnlyList<CardDto>>
{
    public IReadOnlyList<CardDto> GetExamples() => [new(1, "411111XXXXXX1111")];
}

public sealed class CheckoutRequestExample : IMultipleExamplesProvider<CheckoutRequest>
{
    public IEnumerable<SwaggerExample<CheckoutRequest>> GetExamples()
    {
        yield return SwaggerExample.Create("Hosted payment page", new CheckoutRequest(1, 1, null));
        yield return SwaggerExample.Create("Saved card", new CheckoutRequest(1, 3, 1));
    }
}

public sealed class CheckoutResponseExample : IMultipleExamplesProvider<CheckoutResponse>
{
    public IEnumerable<SwaggerExample<CheckoutResponse>> GetExamples()
    {
        yield return SwaggerExample.Create("Hosted page redirect",
            "Redirect the browser to redirectUrl (2C2P hosted payment page).",
            new CheckoutResponse("INV20261017A1B2C3D4", "Pending",
                "https://sandbox-pgw-ui.2c2p.com/payment/4.1/#/token/kSAops9Zwhos8hSTSeLTUcCrXmmxJPRh"));
        yield return SwaggerExample.Create("Saved card (no redirect)",
            "Charged directly with the stored card token; no redirect required.",
            new CheckoutResponse("INV20261017E5F6A7B8", "Succeeded", null));
    }
}

public sealed class CheckoutBadRequestExample : IExamplesProvider<ValidationProblemDetails>
{
    public ValidationProblemDetails GetExamples() => new(new Dictionary<string, string[]>
    {
        ["Idempotency-Key"] = ["Idempotency-Key header is required and must be a UUID."],
    })
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        Title = "One or more validation errors occurred.",
        Status = 400,
    };
}

public sealed class CheckoutNotFoundExample : IExamplesProvider<ProblemDetails>
{
    public ProblemDetails GetExamples() => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        Title = "Not Found",
        Status = 404,
        Detail = "Product 99 was not found.",
    };
}

public sealed class CheckoutConflictExample : IExamplesProvider<ProblemDetails>
{
    public ProblemDetails GetExamples() => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        Title = "Conflict",
        Status = 409,
        Detail = "Idempotency-Key was already used with a different request body.",
    };
}

public sealed class CheckoutUnprocessableExample : IExamplesProvider<ProblemDetails>
{
    public ProblemDetails GetExamples() => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
        Title = "Unprocessable Entity",
        Status = 422,
        Detail = "Saved cards can only be used for recurring products.",
    };
}

public sealed class NotificationRequestExample : IExamplesProvider<TwoC2PNotification>
{
    public TwoC2PNotification GetExamples() => new(
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpbnZvaWNlTm8iOiJJTlYyMDI2MTAxN0ExQjJDM0Q0IiwicmVzcENvZGUiOiIwMDAwIn0.c2lnbmF0dXJl");
}

public sealed class NotificationBadRequestExample : IExamplesProvider<ProblemDetails>
{
    public ProblemDetails GetExamples() => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        Title = "Invalid request",
        Status = 400,
        Detail = "payload is required.",
    };
}

public sealed class TransactionExample : IExamplesProvider<TransactionDto>
{
    public TransactionDto GetExamples() => new(
        "INV20261017A1B2C3D4", "Succeeded", 25.00m, "SGD", 1, 1, "Day Pass", "HostedPage", "CC",
        "411111XXXXXX1111", "0000", "Success", new DateTime(2026, 10, 17, 9, 30, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 17, 9, 31, 12, DateTimeKind.Utc), "Aung Min", "aung.min@example.com", "OneTime");
}

public sealed class TransactionPageExample : IExamplesProvider<PagedResult<TransactionDto>>
{
    public PagedResult<TransactionDto> GetExamples() => new(
    [
        new TransactionExample().GetExamples(),
        new("INV20261017E5F6A7B8", "Pending", 99.00m, "SGD", 2, 3, "Monthly Membership", "SavedCard", null, null,
            null, null, new DateTime(2026, 10, 17, 9, 15, 0, DateTimeKind.Utc), null, "Mei Ling Tan",
            "meiling.tan@example.com", "Recurring"),
    ], 1, 10, 42, 5);
}

public sealed class TransactionListBadRequestExample : IExamplesProvider<ProblemDetails>
{
    public ProblemDetails GetExamples() => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        Title = "Invalid request",
        Status = 400,
        Detail = "pageSize must be between 1 and 100.",
    };
}

public sealed class TransactionNotFoundExample : IExamplesProvider<ProblemDetails>
{
    public ProblemDetails GetExamples() => new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        Title = "Not Found",
        Status = 404,
        Detail = "Transaction 'INV00000000NOPE' was not found.",
    };
}
