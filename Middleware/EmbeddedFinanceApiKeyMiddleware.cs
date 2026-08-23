using KorridorX.Infrastructure;
using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Middleware;

public sealed class EmbeddedFinanceApiKeyMiddleware
{
    public const string HeaderName = "X-KorridorX-Key";
    public const string RoutePrefix = "/api/v1/embedded";
    private readonly RequestDelegate _next;
    public EmbeddedFinanceApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IEmbeddedFinanceCredentialAuthenticator authenticator)
    {
        if (!context.Request.Path.StartsWithSegments(RoutePrefix, StringComparison.OrdinalIgnoreCase)) { await _next(context); return; }
        var apiKey = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey)) { await RejectAsync(context, "Embedded Finance API key is required."); return; }
        var principal = await authenticator.AuthenticateAsync(apiKey, context.Connection.RemoteIpAddress?.ToString(), context.RequestAborted);
        if (principal is null) { await RejectAsync(context, "Embedded Finance API key is invalid or not authorized."); return; }
        context.Items[HttpEmbeddedFinanceContextAccessor.HttpContextItemKey] = principal;
        await _next(context);
    }

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponses.Fail(message, "INVALID_API_CREDENTIAL"), context.RequestAborted);
    }
}
