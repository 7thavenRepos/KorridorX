using KorridorX.Configuration;
using KorridorX.Services.Support;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public sealed class SupportSlaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SupportOptions _options;
    private readonly ILogger<SupportSlaWorker> _logger;

    public SupportSlaWorker(IServiceScopeFactory scopeFactory, IOptions<SupportOptions> options, ILogger<SupportSlaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SlaWorkerEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SlaWorkerIntervalMinutes));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAdminSupportService>();
                var processed = await service.ProcessSlaBreachesAsync(100, stoppingToken);
                if (processed > 0) _logger.LogWarning("Processed {Count} support SLA breaches/escalations.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Support SLA worker failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
