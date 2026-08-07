using KorridorX.Configuration;
using KorridorX.Services.Compliance;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public sealed class ComplianceRescreeningWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ComplianceScreeningOptions _options;
    private readonly ILogger<ComplianceRescreeningWorker> _logger;

    public ComplianceRescreeningWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ComplianceScreeningOptions> options,
        ILogger<ComplianceRescreeningWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RescreeningWorkerEnabled)
        {
            _logger.LogInformation("Compliance rescreening worker is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.RescreeningIntervalHours));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IComplianceScreeningService>();
                var processed = await service.RunDueRescreeningAsync(_options.RescreeningBatchSize, stoppingToken);
                _logger.LogInformation("Compliance rescreening completed. Processed {ProcessedCount} subjects.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compliance rescreening failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
