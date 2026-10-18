using Checkout.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Errors;

/// <summary>Maps application exceptions to RFC 7807 responses; unknown errors become a generic 500.</summary>
public sealed class ExceptionMapping(IProblemDetailsService problemDetails, ILogger<ExceptionMapping> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            InvalidSignatureException => (StatusCodes.Status400BadRequest, "Invalid signature"),
            ConflictException => (StatusCodes.Status409Conflict, "Request in progress"),
            IdempotencyMismatchException => (StatusCodes.Status422UnprocessableEntity, "Idempotency key mismatch"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        // The ExceptionHandlerMiddleware already logs the exception with its stack trace.
        if (status != StatusCodes.Status500InternalServerError)
            logger.LogInformation("Request failed with {Status}: {Message}", status, exception.Message);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception is AppException ? exception.Message : "An unexpected error occurred.",
            },
        });
    }
}
