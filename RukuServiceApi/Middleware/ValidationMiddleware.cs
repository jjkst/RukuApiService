using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using RukuServiceApi.Models;

namespace RukuServiceApi.Middleware;

public class ValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationMiddleware> _logger;

    public ValidationMiddleware(RequestDelegate next, ILogger<ValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only validate JSON for POST/PUT requests with JSON content type
        if (context.Request.Method == "POST" || context.Request.Method == "PUT")
        {
            var contentType = context.Request.ContentType?.ToLowerInvariant() ?? string.Empty;
            
            // Skip JSON validation for:
            // - multipart/form-data (file uploads)
            // - application/x-www-form-urlencoded (form submissions)
            // - text/plain and other non-JSON content types
            // Only validate if Content-Type is application/json or not set (defaults to JSON for API)
            var shouldValidateJson = string.IsNullOrEmpty(contentType) 
                || contentType.Contains("application/json")
                || contentType.Contains("application/*")
                || contentType.Contains("text/json");

            if (shouldValidateJson)
            {
                // Read the request body
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        // Basic JSON validation
                        JsonDocument.Parse(body);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(
                            "Invalid JSON format in request to {Path}: {Error}",
                            context.Request.Path,
                            ex.Message
                        );

                        var errorResponse = new ErrorResponse
                        {
                            Message = "Invalid JSON format",
                            StatusCode = (int)HttpStatusCode.BadRequest,
                            Path = context.Request.Path,
                            CorrelationId = context.TraceIdentifier,
                        };

                        context.Response.StatusCode = errorResponse.StatusCode;
                        context.Response.ContentType = "application/json";

                        var jsonResponse = JsonSerializer.Serialize(
                            errorResponse,
                            new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            }
                        );

                        await context.Response.WriteAsync(jsonResponse);
                        return;
                    }
                }
            }
            else
            {
                _logger.LogDebug(
                    "Skipping JSON validation for Content-Type: {ContentType} on path: {Path}",
                    contentType,
                    context.Request.Path
                );
            }
        }

        await _next(context);
    }
}
