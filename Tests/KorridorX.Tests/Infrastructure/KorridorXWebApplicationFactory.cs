using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using KorridorX.Services.Compliance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KorridorX.Tests.Infrastructure;

public sealed class KorridorXWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var unavailableDatabasePassword =
                nameof(KorridorXWebApplicationFactory);

            var databaseConnection = DatabaseIntegrationTestEnvironment.ConnectionString
                ?? $"Host=127.0.0.1;Port=1;Database=korridorx_tests;Username=test;Password={unavailableDatabasePassword};Timeout=1;Command Timeout=1";

            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = databaseConnection,
                ["Jwt:Issuer"] = "KorridorX.Tests",
                ["Jwt:Audience"] = "KorridorX.Tests.Clients",
                ["Jwt:Key"] = "integration-test-key-that-is-longer-than-thirty-two-characters",
                ["Hosting:AllowedOrigins:0"] = "http://localhost:4200",
                ["Hosting:RequireHttpsRedirection"] = "false",
                ["Hosting:SwaggerEnabled"] = "false",
                ["Hosting:JsonConsoleLogging"] = "false",
                ["Blaaiz:IsEnabled"] = "false",
                ["ComplianceScreening:IsEnabled"] = "false",
                ["ComplianceScreening:RescreeningWorkerEnabled"] = "false",
                ["ComplianceScreening:TransactionMonitoring:IsEnabled"] = "false",
                ["Security:TransferRisk:IsEnabled"] = "false",
                ["Security:RateLimits:GlobalPermitLimit"] = "1000",
                ["Security:RateLimits:AuthenticationPermitLimit"] = "1000",
                ["Security:RateLimits:SensitivePermitLimit"] = "1000",
                ["Security:RateLimits:WebhookPermitLimit"] = "1000",
                ["Security:Accounts:RequireConfirmedEmail"] = "true",
                ["Security:Accounts:FrontendBaseUrl"] = "http://localhost:4200",
                ["Security:Mfa:CodeReplayPepper"] =
                    "integration-test-mfa-replay-pepper-longer-than-thirty-two-characters",
                ["NotificationDelivery:WorkerEnabled"] = "false",
                ["DataRetention:WorkerEnabled"] = "false",
                ["Support:SlaWorkerEnabled"] = "false",
                ["Treasury:WalletSyncWorkerEnabled"] = "false",
                ["Accounting:SyncWorkerEnabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Database-backed RC tests exercise the core transfer workflow without
            // contacting an external screening provider. Screening behavior itself
            // is covered by dedicated provider/parser/unit tests.
            services.RemoveAll<IComplianceScreeningService>();
            services.AddScoped<IComplianceScreeningService, NoOpComplianceScreeningService>();
        });
    }
}
