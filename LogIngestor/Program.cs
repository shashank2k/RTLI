using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using LogIngestor.Messaging;
using LogIngestor.Middleware;
using LogIngestor.Models;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Options;
using LogIngestor.Endpoints;
using LogIngestor.Hosting;

Log.Logger = new LoggerConfiguration()
.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
.Enrich.WithProperty("Application", "LogIngestor")
.WriteTo.Console()
.CreateLogger();

try
{
Log.Information("Starting LogIngestor");

var builder = WebApplication.CreateBuilder(args);

// Serilog first
builder.Services.AddSerilog(); // routes ILogger<T> to Serilog​

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bind RabbitOptions explicitly ONCE
builder.Services.Configure<RabbitOptions>(
builder.Configuration.GetSection("RabbitMq")); // explicit generic avoids CS0411​

// Validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LogIngestor.Validation.LogDtoValidator>();

// Messaging registrations ONCE
builder.Services.AddSingleton<RabbitConnectionFactory>();
builder.Services.AddSingleton<RabbitPublisher>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Serilog HTTP pipeline event (single per request)
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (ctx, http) =>
    {
        ctx.Set("ClientIP", http.Connection.RemoteIpAddress?.ToString());
        ctx.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        ctx.Set("CorrelationId", http.TraceIdentifier);
        ctx.Set("ContentLength", http.Request.ContentLength ?? 0);
    };
});

// Custom enrichment middleware for correlation and size metrics
app.UseMiddleware<RequestEnrichmentMiddleware>();

// Limits (optional)
app.Use(async (context, next) =>
{
    // Reject giant bodies (> 64 KB) to protect the queue
    var len = context.Request.ContentLength ?? 0;
    if (len > 64 * 1024)
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
    else
        await next();
});

app.MapLogIngest(); // from Endpoints/LogEndpoints.cs​
app.MapHealth(); // from Hosting/HealthChecks.cs​

await app.RunAsync();
}
catch (Exception ex)
{
Log.Fatal(ex, "Fatal exception starting LogIngestor");
}
finally
{
await Log.CloseAndFlushAsync();
}

