using KorridorX.Data;
using KorridorX.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests.Infrastructure;

public sealed class ReleaseCandidateDatabaseFixture : IAsyncLifetime
{
    public KorridorXWebApplicationFactory Factory { get; } = new();

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task InitializeAsync()
    {
        if (!DatabaseIntegrationTestEnvironment.IsConfigured)
            return;

        // The configured database MUST be disposable. Resetting the public schema
        // gives every RC test run a deterministic database without requiring the
        // test suite to know the developer's local database name or credentials.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");

            await db.Database.MigrateAsync();
        }

        await RoleSeeder.SeedRolesAsync(Factory.Services);
        await LookupSeeder.SeedAsync(Factory.Services);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("ReleaseCandidateDatabase", DisableParallelization = true)]
public sealed class ReleaseCandidateDatabaseCollection : ICollectionFixture<ReleaseCandidateDatabaseFixture>
{
    public const string Name = "ReleaseCandidateDatabase";
}
