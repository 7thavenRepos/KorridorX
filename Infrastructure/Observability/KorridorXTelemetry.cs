using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KorridorX.Infrastructure.Observability;

public static class KorridorXTelemetry
{
    public const string ServiceName = "KorridorX.Api";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    public static readonly Histogram<double> HttpRequestDurationMilliseconds =
        Meter.CreateHistogram<double>(
            "korridorx.http.server.duration",
            unit: "ms",
            description: "Duration of KorridorX HTTP requests.");

    public static readonly Counter<long> HttpRequestFailures =
        Meter.CreateCounter<long>(
            "korridorx.http.server.failures",
            unit: "request",
            description: "Number of KorridorX HTTP requests returning 5xx responses.");
}
