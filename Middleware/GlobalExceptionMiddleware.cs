using System.Net;
using System.Text.Json;
using KorridorX.Infrastructure;
using KorridorX.Exceptions;
using Microsoft.EntityFrameworkCore;

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
        catch (ForbiddenException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.Forbidden,
                "FORBIDDEN",
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                ex.Message);
        }
        catch (ComplianceLimitExceededException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.UnprocessableEntity,
                ex.Code,
                ex.Message);
        }
        catch (ComplianceHoldException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.Conflict,
                "COMPLIANCE_HOLD",
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
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "A concurrent update conflict occurred.");

            await WriteErrorAsync(
                context,
                HttpStatusCode.Conflict,
                "CONCURRENT_UPDATE",
                "This record changed while your request was being processed. Refresh and try again.");
        }
        catch (JsonException ex)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.BadRequest,
                "INVALID_JSON",
                "The request contains invalid JSON.",
                ex.Message);
        }
        catch (ProviderIntegrationException ex)
        {
            _logger.LogError(
                ex,
                "Provider integration failed. RequestLogId: {RequestLogId}, ProviderStatusCode: {ProviderStatusCode}",
                ex.RequestLogId,
                ex.ProviderStatusCode);

            await WriteErrorAsync(
                context,
                HttpStatusCode.BadGateway,
                "PROVIDER_ERROR",
                ex.Message,
                new
                {
                    requestLogId = ex.RequestLogId,
                    providerStatusCode = ex.ProviderStatusCode
                });
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
        string message,
        object? details = null)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        object responseDetails;

        if (details is null)
        {
            responseDetails = new
            {
                correlationId = context.TraceIdentifier
            };
        }
        else
        {
            responseDetails = new
            {
                correlationId = context.TraceIdentifier,
                context = details
            };
        }

        var response = ApiResponses.Fail(message, code, responseDetails);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}