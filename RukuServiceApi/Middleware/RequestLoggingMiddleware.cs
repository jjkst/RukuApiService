using Serilog;

namespace RukuServiceApi.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var correlationId = context.TraceIdentifier;

        // Log request path only (exclude query string to avoid logging sensitive data)
        Log.Information(
            "Request started: {Method} {Path} from {RemoteIp} - CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Connection.RemoteIpAddress,
            correlationId
        );

        // Add correlation ID to response headers
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Log response (path only, no query string)
        Log.Information(
            "Request completed: {Method} {Path} - Status: {StatusCode} - Duration: {Duration}ms - CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            duration.TotalMilliseconds,
            correlationId
        );

        // Copy response back to original stream
        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);
    }
}
