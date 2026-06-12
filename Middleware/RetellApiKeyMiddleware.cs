using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetellIntegrationApi.Configuration;

namespace RetellIntegrationApi.Middleware;

/// <summary>
/// Middleware to secure the Retell webhook endpoint.
/// Compares the provided 'x-api-key' header (or 'api_key' query parameter) with the Retell:WebhookApiKey value.
/// </summary>
public sealed class RetellApiKeyMiddleware
{
    private static readonly PathString RetellWebhookPath = new("/api/retell/webhook");

    private readonly RequestDelegate _next;
    private readonly RetellOptions _retellOptions;
    private readonly ILogger<RetellApiKeyMiddleware> _logger;

    public RetellApiKeyMiddleware(
        RequestDelegate next,
        IOptions<RetellOptions> retellOptions,
        ILogger<RetellApiKeyMiddleware> logger)
    {
        _next = next;
        _retellOptions = retellOptions.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enforce API key authentication on the webhook endpoint
        if (!context.Request.Path.Equals(RetellWebhookPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_retellOptions.WebhookApiKey))
        {
            _logger.LogError("Retell webhook API key is not configured in application settings.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Retell webhook API key is not configured on the server." });
            return;
        }

        // Validate via header first, fallback to query string param
        var providedApiKey = context.Request.Headers["x-api-key"].ToString();
        if (string.IsNullOrEmpty(providedApiKey))
        {
            providedApiKey = context.Request.Query["api_key"].ToString();
        }

        if (!string.Equals(providedApiKey, _retellOptions.WebhookApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected Retell webhook request for {Path} due to missing or invalid x-api-key.", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. Invalid x-api-key." });
            return;
        }

        _logger.LogInformation("Authenticated Retell webhook request successfully for {Path}", context.Request.Path);
        await _next(context);
    }
}
