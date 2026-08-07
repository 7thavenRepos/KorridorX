using KorridorX.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace KorridorX.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const int MaxCorrelationIdLength = 100;
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly string _headerName;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger,
        IOptions<HostingOptions> options)
    {
        _next = next;
        _logger = logger;
        _headerName = options.Value.CorrelationHeaderName;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[_headerName].ToString());
        context.TraceIdentifier = correlationId;

        Activity.Current?.SetTag("korridorx.correlation_id", correlationId);
        Activity.Current?.AddBaggage("correlation.id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[_headerName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(string suppliedValue)
    {
        var candidate = suppliedValue.Trim();

        if (candidate.Length is > 0 and <= MaxCorrelationIdLength &&
            candidate.All(IsAllowedCharacter))
        {
            return candidate;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsAllowedCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '-' or '_' or '.';
}
