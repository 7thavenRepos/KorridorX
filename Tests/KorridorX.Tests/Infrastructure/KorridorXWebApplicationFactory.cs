using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace KorridorX.Tests.Infrastructure;

public sealed class KorridorXWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=1;Database=korridorx_tests;Username=test;Password=test;Timeout=1;Command Timeout=1",
                ["Jwt:Issuer"] = "KorridorX.Tests",
                ["Jwt:Audience"] = "KorridorX.Tests.Clients",
                ["Jwt:Key"] = "integration-test-key-that-is-longer-than-thirty-two-characters",
                ["Hosting:AllowedOrigins:0"] = "http://localhost:4200",
                ["Hosting:RequireHttpsRedirection"] = "false",
                ["Hosting:SwaggerEnabled"] = "false",
                ["Hosting:JsonConsoleLogging"] = "false",
                ["Blaaiz:IsEnabled"] = "false",
                ["NotificationDelivery:WorkerEnabled"] = "false"
            });
        });
    }
}
