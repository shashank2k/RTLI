using System.Text;
using FluentValidation;
using LogIngestor.Messaging;
using LogIngestor.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LogIngestor.Endpoints;

public static class LogEndpoints
{
public static IEndpointRouteBuilder MapLogIngest(this IEndpointRouteBuilder app)
{
app.MapPost("/log", async (HttpContext http, LogDto dto, IValidator<LogDto> validator, RabbitPublisher publisher, ILoggerFactory lf) =>
{
var logger = lf.CreateLogger("Ingestor");
var result = await validator.ValidateAsync(dto);
if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());        
http.Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
            http.Request.Body.Position = 0;
        }
        if (string.IsNullOrWhiteSpace(rawBody))
        rawBody = System.Text.Json.JsonSerializer.Serialize(dto);

        await publisher.PublishAsync(rawBody);
        logger.LogInformation("Published {Bytes} bytes to queue", Encoding.UTF8.GetByteCount(rawBody));

        await publisher.PublishAsync(rawBody);
        logger.LogInformation("Queued log event CorrelationId={CorrelationId}", http.TraceIdentifier);

        return Results.Accepted("/log", new { status = "queued", correlationId = http.TraceIdentifier });
    })
    .WithSummary("Ingest a log event")
    .WithDescription("Validates JSON and enqueues to RabbitMQ; returns 202 Accepted.")
    .Produces(StatusCodes.Status202Accepted)
    .ProducesProblem(StatusCodes.Status400BadRequest);

    return app;
}
}