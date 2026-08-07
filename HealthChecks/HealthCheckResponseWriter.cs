using KorridorX.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace KorridorX.HealthChecks;

public static class HealthCheckResponseWriter
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var data = new
        {
            service = "KorridorX API",
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            durationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            correlationId = context.TraceIdentifier,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
            })
        };

        var response = ApiResponses.Ok(data, "Health check completed.");
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
