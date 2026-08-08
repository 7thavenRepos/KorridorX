using Xunit;

namespace KorridorX.Tests.Infrastructure;

public sealed class DatabaseIntegrationFactAttribute : FactAttribute
{
    public DatabaseIntegrationFactAttribute()
    {
        if (!DatabaseIntegrationTestEnvironment.IsConfigured)
        {
            Skip =
                "Database integration test skipped. Set KORRIDORX_TEST_CONNECTION_STRING " +
                "to a disposable PostgreSQL database to enable release-candidate integration tests.";
        }
    }
}

public static class DatabaseIntegrationTestEnvironment
{
    public const string ConnectionStringVariable = "KORRIDORX_TEST_CONNECTION_STRING";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable);

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString);
}
