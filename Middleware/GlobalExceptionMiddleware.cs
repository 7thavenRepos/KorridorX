using System.Net;
using System.Text.Json;
using KorridorX.Infrastructure;

namespace KorridorX.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.BadRequest,
                "INVALID_OPERATION",
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");

            await WriteErrorAsync(
                context,
                HttpStatusCode.InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string code,
        string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponses.Fail(message, code);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}