using KorridorX.Configuration;
using KorridorX.Services.Compliance;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public sealed class DataRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<DataRetentionWorker> _logger;

    public DataRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled)
        {
            _logger.LogInformation("Data retention worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
                var count = await service.ExecuteActivePoliciesAsync(_options.BatchSize, stoppingToken);
                _logger.LogInformation("Data retention worker executed {PolicyCount} active policies.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data retention worker execution failed.");
            }

            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }
    }
}
