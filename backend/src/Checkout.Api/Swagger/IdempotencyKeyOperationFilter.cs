using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Checkout.Api.Swagger;

/// <summary>
/// Marks the Idempotency-Key header as required (with a UUID example) on actions that bind it,
/// and documents the 302 redirect of the 2C2P browser return endpoint.
/// </summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    public const string HeaderName = "Idempotency-Key";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var bindsHeader = context.ApiDescription.ParameterDescriptions.Any(p =>
            p.Source.Id == "Header" && string.Equals(p.Name, HeaderName, StringComparison.OrdinalIgnoreCase));
        if (bindsHeader)
        {
            operation.Parameters ??= [];
            var existing = operation.Parameters.FirstOrDefault(p =>
                p.In == ParameterLocation.Header && string.Equals(p.Name, HeaderName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) operation.Parameters.Remove(existing);
            operation.Parameters.Insert(0, new OpenApiParameter
            {
                Name = HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Client-generated UUID identifying this checkout attempt. Retrying with the same key "
                    + "and body replays the original result; the same key with a different body returns 409.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                Example = JsonValue.Create("3f2b8c1e-9a4d-4e6f-b1c2-7d8e9f0a1b2c"),
            });
        }

        if (context.ApiDescription.RelativePath?.EndsWith("2c2p/return", StringComparison.OrdinalIgnoreCase) == true
            && operation.Responses?.TryGetValue("302", out var found) == true && found is OpenApiResponse response)
        {
            response.Description = "Redirects the shopper to the frontend result page "
                + "({App:FrontendBaseUrl}/result?invoiceNo=...).";
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers["Location"] = new OpenApiHeader
            {
                Description = "Frontend result page URL.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
            };
        }

        // The acknowledgement body is an empty object; Swashbuckle drops an empty inline example, so name it.
        if (context.ApiDescription.RelativePath?.EndsWith("2c2p/notification", StringComparison.OrdinalIgnoreCase) == true
            && operation.Responses?.TryGetValue("200", out var ok) == true
            && ok.Content?.TryGetValue("application/json", out var media) == true && media is OpenApiMediaType okMedia)
            okMedia.Examples = new Dictionary<string, IOpenApiExample>
            {
                ["Acknowledged"] = new OpenApiExample { Summary = "Notification accepted", Value = new JsonObject() },
            };
    }
}
