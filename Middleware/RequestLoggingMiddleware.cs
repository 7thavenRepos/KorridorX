using KorridorX.Configuration;
using KorridorX.Infrastructure.Observability;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;

namespace KorridorX.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly int _slowRequestThresholdMilliseconds;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<HostingOptions> options)
    {
        _next = next;
        _logger = logger;
        _slowRequestThresholdMilliseconds = options.Value.SlowRequestThresholdMilliseconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        await _next(context);

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var statusCode = context.Response.StatusCode;
        var route = context.Request.Path.Value ?? "/";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        KorridorXTelemetry.HttpRequestDurationMilliseconds.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("http.request.method", context.Request.Method),
            new KeyValuePair<string, object?>("http.response.status_code", statusCode),
            new KeyValuePair<string, object?>("http.route", route));

        if (statusCode >= 500)
        {
            KorridorXTelemetry.HttpRequestFailures.Add(
                1,
                new KeyValuePair<string, object?>("http.request.method", context.Request.Method),
                new KeyValuePair<string, object?>("http.route", route));
        }

        if (route.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Health request {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds:F2} ms.",
                context.Request.Method,
                route,
                statusCode,
                elapsedMilliseconds);
            return;
        }

        if (statusCode >= 500 || elapsedMilliseconds >= _slowRequestThresholdMilliseconds)
        {
            _logger.LogWarning(
                "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds:F2} ms for user {UserId}.",
                context.Request.Method,
                route,
                statusCode,
                elapsedMilliseconds,
                userId);
            return;
        }

        _logger.LogInformation(
            "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds:F2} ms for user {UserId}.",
            context.Request.Method,
            route,
            statusCode,
            elapsedMilliseconds,
            userId);
    }
}
