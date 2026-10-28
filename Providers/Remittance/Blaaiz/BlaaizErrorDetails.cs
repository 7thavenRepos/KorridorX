using System.Text.Json;
using System.Text.RegularExpressions;

namespace KorridorX.Providers.Remittance.Blaaiz;

// Interpret errors without allowing their JSON shape to hide the original HTTP
// status/body. This is diagnostics only: it never changes retry or payout rules.
public sealed record BlaaizErrorDetails(string Message, string? Code, string? ProviderRequestReference)
{
    public string Format() => Safe(
        (Code is null ? "" : $"[{Code}] ") + Message +
        (ProviderRequestReference is null ? "" : $" (Blaaiz request: {ProviderRequestReference})"));

    public static BlaaizErrorDetails Read(string? body, string fallback, string? requestReference = null)
    {
        string? message = null, code = null, reference = Identifier(requestReference);
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var nodes = new List<JsonElement> { document.RootElement };
                for (var i = 0; i < nodes.Count && i < 6; i++)
                {
                    var node = nodes[i];
                    if (node.ValueKind != JsonValueKind.Object) continue;
                    message ??= Text(node, "message", "error_description", "detail", "reason");
                    code ??= Identifier(Text(node, "error_code", "errorCode", "code"));
                    reference ??= Identifier(Text(node, "request_id", "requestId", "correlation_id", "correlationId"));
                    if (node.TryGetProperty("error", out var error))
                    {
                        if (error.ValueKind == JsonValueKind.String) message ??= error.GetString();
                        else if (error.ValueKind == JsonValueKind.Object) nodes.Add(error);
                    }
                    if (node.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object) nodes.Add(data);
                    if (node.TryGetProperty("errors", out var errors)) Messages(errors, "", details, 0);
                    if (node.TryGetProperty("validation_errors", out var validation)) Messages(validation, "", details, 0);
                }
            }
            catch (JsonException) { /* The HTTP status and audited body remain authoritative. */ }
        }
        var main = string.IsNullOrWhiteSpace(message) ? fallback : message;
        if (details.Count > 0) main += " " + string.Join("; ", details.Distinct().Take(8));
        return new(Safe(main), code, reference);
    }

    public static string? ResponseReference(HttpResponseMessage response)
    {
        foreach (var name in new[] { "X-Request-ID", "X-Correlation-ID", "Request-ID" })
            if (response.Headers.TryGetValues(name, out var values))
                foreach (var value in values)
                    if (Identifier(value) is { } id) return id;
        return null;
    }

    public static string Safe(string? value, params string?[] secrets)
    {
        var text = value ?? "";
        foreach (var secret in secrets)
            if (!string.IsNullOrEmpty(secret)) text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
        if (text.Length > 6000) text = text[..6000];
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        text = Regex.Replace(text, @"(?i)\bBearer\s+\S+", "Bearer [redacted]");
        text = Regex.Replace(text,
            @"(?i)(\b(?:authorization|client_secret|access_token|refresh_token|password|api_key)\b[""']?\s*[:=]\s*[""']?)([^""'\s,;&}]+)",
            "$1[redacted]");
        text = Regex.Replace(text, @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[redacted email]");
        text = Regex.Replace(text, @"(?i)\b[A-Z]{2}\d{2}[A-Z0-9]{10,30}\b", "[redacted IBAN]");
        text = Regex.Replace(text, @"\b\d{6,}\b", "[redacted number]");
        return text.Length <= 1800 ? text : text[..1800];
    }

    private static string? Identifier(string? value) =>
        value is not null && Regex.IsMatch(value, @"\A[A-Za-z0-9_.:-]{1,160}\z") ? value : null;

    private static string? Text(JsonElement node, params string[] names)
    {
        foreach (var name in names)
            if (node.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
        return null;
    }

    private static void Messages(JsonElement node, string path, List<string> result, int depth)
    {
        if (depth > 4 || result.Count >= 8) return;
        if (node.ValueKind == JsonValueKind.String)
        {
            var text = node.GetString();
            if (!string.IsNullOrWhiteSpace(text)) result.Add(path.Length == 0 ? text : path + ": " + text);
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) Messages(item, path, result, depth + 1);
        else if (node.ValueKind == JsonValueKind.Object)
            foreach (var item in node.EnumerateObject()) Messages(item.Value, item.Name, result, depth + 1);
    }
}
