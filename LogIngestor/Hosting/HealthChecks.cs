using LogIngestor.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LogIngestor.Hosting;

public static class HealthChecks
{
public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
{
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
.WithSummary("Liveness"); // process is up    
app.MapGet("/health/ready", (RabbitPublisher pub) =>
        app.MapGet("/health/ready", (RabbitPublisher pub) =>
    {
        return pub.IsHealthy ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503);
    })
    .WithSummary("Readiness"));  // dependencies are ready

    return app;
}
}