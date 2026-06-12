using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RetellIntegrationApi.Middleware;

/// <summary>
/// Middleware that logs incoming HTTP requests, their outgoing status codes, and execution durations.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("HTTP Request Started: {Method} {Path}", 
            context.Request.Method, context.Request.Path);

        try
        {
            await _next(context);
            stopwatch.Stop();
            
            _logger.LogInformation("HTTP Request Finished: {Method} {Path} responded with status {StatusCode} in {ElapsedMs}ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch
        {
            stopwatch.Stop();
            _logger.LogWarning("HTTP Request Failed: {Method} {Path} terminated with an unhandled exception after {ElapsedMs}ms",
                context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
