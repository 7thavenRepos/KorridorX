using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KorridorX.Data;

/// <summary>
/// Creates the EF Core context for design-time commands such as
/// Add-Migration, Update-Database, and dotnet ef migrations add.
///
/// Design-time context creation is deliberately independent from the complete
/// runtime dependency-injection graph. This prevents unrelated hosted-service
/// or provider validation failures from blocking migrations.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var projectDirectory = ResolveProjectDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: true,
                reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found for EF Core " +
                $"design-time operations. Configuration was loaded from " +
                $"'{projectDirectory}' using environment '{environment}'. " +
                "Set ConnectionStrings:DefaultConnection in " +
                $"appsettings.{environment}.json or set the environment variable " +
                "ConnectionStrings__DefaultConnection.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveProjectDirectory()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(AppDbContextFactory).Assembly.Location)
        };

        foreach (var candidate in candidates)
        {
            var resolved = FindProjectDirectory(candidate);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? FindProjectDirectory(string? startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(startPath);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KorridorX.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
