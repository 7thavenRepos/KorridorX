using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace KorridorX.Services.SearchHelp;
public static class SearchHelpRegistration
{
    public static IServiceCollection AddWorkspaceSearchHelp(this IServiceCollection services)
    {
        services.AddScoped<SearchScopeResolver>();services.AddScoped<SearchRecordCatalog>();
        services.AddScoped<WorkspaceSearchService>();services.AddSingleton<HelpCatalog>();
        services.Configure<RateLimiterOptions>(options => options.AddPolicy("WorkspaceSearch",context =>
            RateLimitPartition.GetFixedWindowLimiter("workspace-search:"+(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"),_ => new FixedWindowRateLimiterOptions {
                    PermitLimit=90,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true })));
        return services;
    }
}
