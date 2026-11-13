using System.Text;
using KorridorX.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace KorridorX.Tests;

public sealed class BusinessTradingFailureResponseTests
{
    [Fact]
    public async Task EF_database_concurrency_fault_is_a_sanitized_server_error()
    {
        const string internalMessage = "A second operation was started on this context instance before a previous operation completed. " +
                                       "This is usually caused by different threads concurrently using the same instance of DbContext.";
        var middleware = new GlobalExceptionMiddleware(
            _ => Task.FromException(new InvalidOperationException(internalMessage)),
            NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext { TraceIdentifier = "test-trading-failure" };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("DATABASE_OPERATION_FAILED", body);
        Assert.Contains("test-trading-failure", body);
        Assert.DoesNotContain("DbContext", body);
        Assert.DoesNotContain("A second operation", body);
    }
}
