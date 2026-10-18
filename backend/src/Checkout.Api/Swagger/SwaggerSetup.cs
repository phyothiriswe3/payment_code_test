using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;

namespace Checkout.Api.Swagger;

public static class SwaggerSetup
{
    public static IServiceCollection AddSwaggerDocs(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Rezerv Checkout API",
                Version = "v1",
                Description = "Checkout backed by the 2C2P Payment Gateway (hosted page and saved-card flows).",
            });
            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml)) o.IncludeXmlComments(xml);
            o.ExampleFilters();
            o.OperationFilter<IdempotencyKeyOperationFilter>();
        });
        services.AddSwaggerExamplesFromAssemblyOf<CheckoutResponseExample>();
        return services;
    }

    public static WebApplication UseSwaggerDocs(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Rezerv Checkout API v1"));
        return app;
    }
}
