using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json;
using Checkout.Application;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>2C2P server-to-server notification body.</summary>
/// <param name="Payload">HS256 JWT signed with the merchant secret key.</param>
public sealed record TwoC2PNotification(string? Payload);

/// <summary>Callbacks invoked by 2C2P.</summary>
[ApiController]
[Route("api/payments/2c2p")]
public sealed class TwoC2PController(IPaymentResultService results, IConfiguration config, ILogger<TwoC2PController> logger)
    : ControllerBase
{
    /// <summary>Backend notification (backendReturnUrl). Verifies the JWT and settles the transaction.</summary>
    /// <param name="body">The <c>{payload}</c> envelope.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("notification")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [SwaggerRequestExample(typeof(TwoC2PNotification), typeof(Checkout.Api.Swagger.NotificationRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(Checkout.Api.Swagger.NotificationBadRequestExample))]
    public async Task<IActionResult> Notification([FromBody] TwoC2PNotification body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Payload))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "payload is required.");

        await results.ApplyNotificationAsync(body.Payload, ct);
        return Ok(new { });
    }

    /// <summary>
    /// Browser return (frontendReturnUrl). 2C2P POSTs a form here; redirects the shopper to the frontend result page.
    /// </summary>
    /// <param name="invoiceNo">Invoice number appended to the return URL.</param>
    [HttpGet("return")]
    [HttpPost("return")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Return([FromQuery] string? invoiceNo)
    {
        var frontend = (config["App:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        invoiceNo ??= await ReadInvoiceFromFormAsync();
        return string.IsNullOrWhiteSpace(invoiceNo)
            ? Redirect(frontend + "/")
            : Redirect($"{frontend}/result/{Uri.EscapeDataString(invoiceNo.Trim())}");
    }

    /// <summary>2C2P may post <c>paymentResponse</c>: base64-encoded JSON containing <c>invoiceNo</c>.</summary>
    private async Task<string?> ReadInvoiceFromFormAsync()
    {
        if (!Request.HasFormContentType) return null;
        try
        {
            var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
            var encoded = form["paymentResponse"].ToString();
            if (string.IsNullOrEmpty(encoded)) return null;

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            return doc.RootElement.TryGetProperty("invoiceNo", out var inv) && inv.ValueKind == JsonValueKind.String
                ? inv.GetString()
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
        {
            logger.LogWarning("Could not read invoiceNo from 2C2P return form: {Error}", ex.Message);
            return null;
        }
    }
}
