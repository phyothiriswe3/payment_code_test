using System.Text.Json.Serialization;
using Checkout.Api.Errors;
using Checkout.Api.Swagger;
using Checkout.Application;
using Checkout.Infrastructure;
using Checkout.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionMapping>();
builder.Services.AddSwaggerDocs();

const string CorsPolicy = "frontend";
var frontendOrigin = (builder.Configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(frontendOrigin, "http://localhost:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (!app.Configuration.GetValue<bool>("Database:SkipMigrate"))
    await app.Services.MigrateDatabaseAsync(app.Logger, app.Lifetime.ApplicationStopping);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwaggerDocs();
app.UseCors(CorsPolicy);
app.MapControllers();

await app.RunAsync();

public partial class Program { }
