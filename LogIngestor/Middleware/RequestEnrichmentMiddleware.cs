using System.Diagnostics;

namespace LogIngestor.Middleware;

public class RequestEnrichmentMiddleware
{
private readonly RequestDelegate _next;
private static readonly ActivitySource Source = new("LogIngestor");public RequestEnrichmentMiddleware(RequestDelegate next) => _next = next;

public async Task Invoke(HttpContext context, ILogger<RequestEnrichmentMiddleware> logger)
{
    var activity = Source.StartActivity("HTTP " + context.Request.Path);
    try
    {
        context.Items["CorrelationId"] = context.TraceIdentifier;
        await _next(context);
    }
    finally
    {
        activity?.Stop();
    }
}
}