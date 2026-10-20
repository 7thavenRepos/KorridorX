using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Providers.Fx;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Fx;

public sealed class FxProviderRateService : IFxProviderRateService, ILiveFxRateService
{
    private readonly AppDbContext _db;
    private readonly IReadOnlyList<IFxRateProvider> _providers;
    private readonly IAuditService _audit;
    private readonly ILogger<FxProviderRateService> _logger;

    public FxProviderRateService(AppDbContext db, IEnumerable<IFxRateProvider> providers,
        IAuditService audit, ILogger<FxProviderRateService> logger)
    {
        _db = db; _providers = providers.ToArray(); _audit = audit; _logger = logger;
    }

    public async Task<FxProviderOptionsDto> GetOptionsAsync(CancellationToken ct = default)
    {
        var instant = await _db.InstantPairs.AsNoTracking().Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.SourceAssetCode, x.DestinationAssetCode, x.Status }).ToListAsync(ct);
        var marketplace = await _db.MarketplacePairs.AsNoTracking().Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.BaseAssetCode, x.QuoteAssetCode, x.Status }).ToListAsync(ct);
        return new FxProviderOptionsDto(instant.Select(x => new FxPairOptionDto("Instant", x.Id, x.Code,
                x.SourceAssetCode, x.DestinationAssetCode, x.Status.ToString()))
            .Concat(marketplace.Select(x => new FxPairOptionDto("Marketplace", x.Id, x.Code,
                x.BaseAssetCode, x.QuoteAssetCode, x.Status.ToString())))
            .OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.DestinationCurrencyCode).ThenBy(x => x.PairType).ToArray(),
            _providers.Select(x => new FxProviderOptionDto(x.ProviderCode, x.ProviderCode, x.IsAvailable)).ToArray());
    }

    public async Task<IReadOnlyList<FxProviderConfigurationDto>> GetConfigurationsAsync(CancellationToken ct = default)
    {
        var options = await GetOptionsAsync(ct);
        var rows = await _db.FxProviderRateConfigurations.AsNoTracking().Where(x => !x.IsDeleted)
            .OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.DestinationCurrencyCode).ThenByDescending(x => x.IsActive)
            .ThenBy(x => x.ProviderCode).ToListAsync(ct);
        return rows.Select(x => ToDto(x, options.Pairs.FirstOrDefault(p => p.PairId == PairId(x) && p.PairType == PairType(x))?.Code)).ToArray();
    }

    public async Task<FxProviderPreviewDto> PreviewAsync(SaveFxProviderConfigurationRequestDto request, CancellationToken ct = default)
    {
        var pair = await ResolvePairAsync(request.PairType, request.PairId, false, false, ct);
        var candidate = FromRequest(request, pair);
        var result = await ReadProviderAsync(candidate, allowNewConnection: true, ct);
        return ToPreview(candidate, result);
    }

    public async Task<FxProviderConfigurationDto> SaveAsync(Guid userId, Guid? id, SaveFxProviderConfigurationRequestDto request, CancellationToken ct = default)
    {
        var pair = await ResolvePairAsync(request.PairType, request.PairId, false, false, ct);
        var candidate = FromRequest(request, pair);
        var dto = await WithPairLockAsync(pair.SourceCurrencyCode, pair.DestinationCurrencyCode, async () =>
        {
            var lockedPair = await ResolvePairAsync(request.PairType, request.PairId, false, true, ct);
            if (lockedPair.SourceCurrencyCode != pair.SourceCurrencyCode || lockedPair.DestinationCurrencyCode != pair.DestinationCurrencyCode)
                throw new InvalidOperationException("The selected pair changed while saving. Reload and try again.");
            FxProviderRateConfiguration config;
            if (id.HasValue)
            {
                config = await CurrentAsync(id.Value, ct);
                EnsureRevision(config, request.Revision);
                if (config.IsActive) throw new InvalidOperationException("An active FX configuration cannot be edited. Save a new draft and activate it, or deactivate this configuration first.");
                if (PairType(config) != request.PairType || PairId(config) != request.PairId ||
                    config.SourceCurrencyCode != pair.SourceCurrencyCode || config.DestinationCurrencyCode != pair.DestinationCurrencyCode)
                    throw new InvalidOperationException("The pair on an existing FX configuration cannot be changed.");
                config.Revision++;
                config.LastProviderRate = null; config.LastCustomerRate = null; config.LastSuccessAtUtc = null;
                config.LastObservationStartedAtUtc = null; config.LastProviderTimestampUtc = null;
                config.LastError = null; config.LastAttemptAtUtc = null; config.ActivatedConnectionKey = null;
            }
            else
            {
                config = candidate; config.CreatedByUserId = userId;
                _db.FxProviderRateConfigurations.Add(config);
            }
            config.ProviderCode = candidate.ProviderCode; config.ProviderRateId = candidate.ProviderRateId;
            config.MarkupPercentage = candidate.MarkupPercentage;
            config.MinimumCustomerRate = candidate.MinimumCustomerRate; config.MaximumCustomerRate = candidate.MaximumCustomerRate;
            config.ScheduleMode = candidate.ScheduleMode; config.IntervalMinutes = candidate.IntervalMinutes;
            config.DailyTimes = candidate.DailyTimes; config.TimeZoneId = candidate.TimeZoneId;
            config.NextRefreshAtUtc = null; config.RefreshLeaseId = null; config.RefreshLeaseUntilUtc = null;
            config.LastUpdatedByUserId = userId; config.LastUpdatedAt = DateTime.UtcNow;
            StageAudit(userId, "FxProviderConfigurationSaved", ToDto(config, pair.Code));
            await _db.SaveChangesAsync(ct);
            return ToDto(config, pair.Code);
        }, ct);
        return dto;
    }

    public async Task<FxProviderConfigurationDto> ActivateAsync(Guid userId, Guid id, long revision, CancellationToken ct = default)
    {
        var snapshot = await SnapshotAsync(id, ct);
        EnsureRevision(snapshot, revision);
        await ResolvePairAsync(PairType(snapshot), PairId(snapshot), true, false, ct);
        // A usable live read is required BEFORE the old provider is deactivated.
        var observed = await ReadProviderAsync(snapshot, allowNewConnection: true, ct);
        var dto = await WithPairLockAsync(snapshot.SourceCurrencyCode, snapshot.DestinationCurrencyCode, async () =>
        {
            var current = await CurrentAsync(id, ct);
            EnsureRevision(current, revision);
            await CheckPairAsync(current, ct);
            EnsureConnection(current, observed, requireActivated: false);
            var active = await _db.FxProviderRateConfigurations.Where(x => !x.IsDeleted && x.IsActive &&
                x.SourceCurrencyCode == current.SourceCurrencyCode && x.DestinationCurrencyCode == current.DestinationCurrencyCode).ToListAsync(ct);
            foreach (var old in active)
            {
                if (old.Id != current.Id) await _db.Entry(old).ReloadAsync(ct);
                old.IsActive = false; old.Revision++; old.NextRefreshAtUtc = null;
                old.RefreshLeaseId = null; old.RefreshLeaseUntilUtc = null;
                old.LastUpdatedAt = DateTime.UtcNow; old.LastUpdatedByUserId = userId;
            }
            // Separate statements inside this transaction satisfy the filtered unique index in every EF update order.
            await _db.SaveChangesAsync(ct);
            if (!active.Any(x => x.Id == current.Id)) current.Revision++;
            current.IsActive = true; current.ActivatedConnectionKey = observed.ConnectionKey;
            current.LastUpdatedAt = DateTime.UtcNow; current.LastUpdatedByUserId = userId;
            current.NextRefreshAtUtc = FxRateSchedule.Next(current, observed.ReceivedAtUtc);
            await PublishAsync(current, observed, "Activation", true, ct);
            StageAudit(userId, "FxProviderConfigurationActivated", ToDto(current));
            await _db.SaveChangesAsync(ct);
            return ToDto(current);
        }, ct);
        return dto;
    }

    public async Task<FxProviderConfigurationDto> DeactivateAsync(Guid userId, Guid id, long revision, CancellationToken ct = default)
    {
        var snapshot = await SnapshotAsync(id, ct);
        var dto = await WithPairLockAsync(snapshot.SourceCurrencyCode, snapshot.DestinationCurrencyCode, async () =>
        {
            var config = await CurrentAsync(id, ct);
            EnsureRevision(config, revision);
            config.IsActive = false; config.Revision++; config.NextRefreshAtUtc = null;
            config.RefreshLeaseId = null; config.RefreshLeaseUntilUtc = null;
            config.LastUpdatedAt = DateTime.UtcNow; config.LastUpdatedByUserId = userId;
            var rates = await _db.ExchangeRates.Where(x => x.FxProviderRateConfigurationId == id && x.IsActive && !x.IsDeleted).ToListAsync(ct);
            foreach (var rate in rates) { rate.IsActive = false; rate.EffectiveTo = DateTime.UtcNow; }
            StageAudit(userId, "FxProviderConfigurationDeactivated", ToDto(config));
            await _db.SaveChangesAsync(ct);
            return ToDto(config);
        }, ct);
        return dto;
    }

    public async Task<FxProviderConfigurationDto> RefreshAsync(Guid userId, Guid id, long revision, CancellationToken ct = default)
    {
        return await RefreshCoreAsync(id, revision, null, ct, userId);
    }

    public async Task<T> UseFreshRateAsync<T>(string source, string destination, FxRateUse use,
        Func<ExchangeRate, Guid, CancellationToken, Task<T>> consume, CancellationToken ct = default)
    {
        source = source.Trim().ToUpperInvariant(); destination = destination.Trim().ToUpperInvariant();
        var snapshot = await _db.FxProviderRateConfigurations.AsNoTracking().SingleOrDefaultAsync(x => !x.IsDeleted && x.IsActive &&
            x.SourceCurrencyCode == source && x.DestinationCurrencyCode == destination, ct)
            ?? throw new InvalidOperationException("Live FX pricing is not active for this currency pair. Contact support or ask an administrator to activate its provider configuration.");
        var observed = await ReadProviderAsync(snapshot, allowNewConnection: false, ct);
        if ((use is FxRateUse.Transfer or FxRateUse.Preview) && !observed.SendEnabled)
            throw new InvalidOperationException("The provider has not enabled this FX rate for sending money.");
        if (use == FxRateUse.Instant && !observed.SwapEnabled)
            throw new InvalidOperationException("The provider has not enabled this FX rate for instant conversion.");
        return await WithPairLockAsync(source, destination, async () =>
        {
            var config = await CurrentAsync(snapshot.Id, ct);
            EnsureSameSelection(config, snapshot);
            await CheckPairAsync(config, ct);
            EnsureConnection(config, observed, requireActivated: true);
            var (rate, observation) = await PublishAsync(config, observed, use.ToString(), true, ct);
            var result = await consume(rate!, observation.Id, ct);
            await _db.SaveChangesAsync(ct);
            return result;
        }, ct);
    }

    public async Task RefreshDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.FxProviderRateConfigurations.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive &&
            x.ScheduleMode != "Disabled" && x.NextRefreshAtUtc <= now &&
            (x.RefreshLeaseUntilUtc == null || x.RefreshLeaseUntilUtc <= now))
            .OrderBy(x => x.NextRefreshAtUtc).Take(100).ToListAsync(ct);
        foreach (var candidate in due)
        {
            ct.ThrowIfCancellationRequested();
            var lease = Guid.NewGuid();
            var claimed = await WithPairLockAsync(candidate.SourceCurrencyCode, candidate.DestinationCurrencyCode, async () =>
            {
                var current = await CurrentAsync(candidate.Id, ct);
                if (!current.IsActive || current.ScheduleMode == "Disabled" || current.NextRefreshAtUtc > DateTime.UtcNow ||
                    current.NextRefreshAtUtc == null || current.RefreshLeaseUntilUtc > DateTime.UtcNow) return false;
                current.RefreshLeaseId = lease; current.RefreshLeaseUntilUtc = DateTime.UtcNow.AddMinutes(3);
                await _db.SaveChangesAsync(ct); candidate.Revision = current.Revision;
                return true;
            }, ct);
            if (!claimed) continue;
            try { await RefreshCoreAsync(candidate.Id, candidate.Revision, lease, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Provider errors are already audited with safe details. No secrets/raw responses enter worker logs.
                _logger.LogWarning("Scheduled FX refresh failed for configuration {ConfigurationId}: {ErrorType}.", candidate.Id, ex.GetType().Name);
                await _db.FxProviderRateConfigurations.Where(x => x.Id == candidate.Id && x.IsActive &&
                    x.Revision == candidate.Revision && x.RefreshLeaseId == lease)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.RefreshLeaseId, (Guid?)null)
                        .SetProperty(x => x.RefreshLeaseUntilUtc, (DateTime?)null)
                        .SetProperty(x => x.NextRefreshAtUtc, (DateTime?)DateTime.UtcNow.AddMinutes(5)), ct);
            }
        }
    }

    private async Task<FxProviderConfigurationDto> RefreshCoreAsync(Guid id, long revision, Guid? lease, CancellationToken ct, Guid? userId = null)
    {
        var snapshot = await SnapshotAsync(id, ct);
        EnsureRevision(snapshot, revision);
        if (lease.HasValue && (!snapshot.IsActive || snapshot.RefreshLeaseId != lease))
            throw new InvalidOperationException("The scheduled FX refresh was superseded.");
        var observed = await ReadProviderAsync(snapshot, allowNewConnection: !snapshot.IsActive, ct);
        return await WithPairLockAsync(snapshot.SourceCurrencyCode, snapshot.DestinationCurrencyCode, async () =>
        {
            var current = await CurrentAsync(id, ct);
            EnsureSameSelection(current, snapshot, requireActive: false);
            if (lease.HasValue && (current.RefreshLeaseId != lease || current.RefreshLeaseUntilUtc < DateTime.UtcNow))
                throw new InvalidOperationException("The scheduled FX refresh lease expired or was superseded.");
            await CheckPairAsync(current, ct, requireActive: current.IsActive);
            EnsureConnection(current, observed, requireActivated: current.IsActive);
            await PublishAsync(current, observed, lease.HasValue ? "Scheduled" : "Manual", current.IsActive, ct);
            if (lease.HasValue)
            {
                current.RefreshLeaseId = null; current.RefreshLeaseUntilUtc = null;
                current.NextRefreshAtUtc = FxRateSchedule.Next(current, observed.ReceivedAtUtc);
            }
            StageAudit(userId ?? Guid.Empty, lease.HasValue ? "FxProviderRateScheduledRefresh" : "FxProviderRateRefreshed", ToDto(current));
            await _db.SaveChangesAsync(ct);
            return ToDto(current);
        }, ct);
    }

    private async Task<FxProviderRate> ReadProviderAsync(FxProviderRateConfiguration config, bool allowNewConnection, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("FX provider reads must start outside a database transaction.");
        var started = DateTime.UtcNow;
        try
        {
            var provider = Provider(config.ProviderCode);
            if (!provider.IsAvailable) throw new InvalidOperationException("The FX provider connection is not configured or enabled.");
            var key = provider.ConnectionKey;
            if (!allowNewConnection && config.ActivatedConnectionKey != key)
                throw new InvalidOperationException("The FX provider connection changed. An administrator must activate the configuration again.");
            var observed = await provider.GetRateAsync(config.SourceCurrencyCode, config.DestinationCurrencyCode, config.ProviderRateId, ct);
            if (observed.ConnectionKey != key || provider.ConnectionKey != key ||
                observed.SourceCurrencyCode != config.SourceCurrencyCode || observed.DestinationCurrencyCode != config.DestinationCurrencyCode ||
                observed.ReceivedAtUtc < observed.RequestedAtUtc || observed.RequestedAtUtc < started.AddSeconds(-1))
                throw new InvalidOperationException("The provider rate could not be verified for this check. Retry the request.");
            FxPricing.Calculate(observed.Rate, config.MarkupPercentage, config.MinimumCustomerRate, config.MaximumCustomerRate);
            return observed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Do not SaveChanges on the caller's unit of work on failure (batch imports may have pending rows).
            if (config.CreatedByUserId.HasValue || await _db.FxProviderRateConfigurations.AsNoTracking().AnyAsync(x => x.Id == config.Id, ct))
            {
                var error = ex is InvalidOperationException ? ex.Message : "The provider rate request failed. Check connection access and provider availability.";
                error = error.Length <= 500 ? error : error[..500];
                await _db.FxProviderRateConfigurations.Where(x => x.Id == config.Id && x.Revision == config.Revision &&
                    (x.LastAttemptAtUtc == null || x.LastAttemptAtUtc <= started))
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.LastAttemptAtUtc, (DateTime?)started).SetProperty(x => x.LastError, error), ct);
            }
            if (ex is InvalidOperationException) throw;
            throw new InvalidOperationException("A fresh FX rate is unavailable. Please retry shortly or contact support.", ex);
        }
    }

    private async Task<(ExchangeRate? Rate, FxRateObservation Observation)> PublishAsync(
        FxProviderRateConfiguration config, FxProviderRate observed, string purpose, bool publish, CancellationToken ct)
    {
        if (config.LastObservationStartedAtUtc > observed.RequestedAtUtc)
            throw new InvalidOperationException("A newer FX check completed while this request was running. Please retry.");
        var customer = FxPricing.Calculate(observed.Rate, config.MarkupPercentage, config.MinimumCustomerRate, config.MaximumCustomerRate);
        var observation = new FxRateObservation
        {
            ConfigurationId = config.Id, ConfigurationRevision = config.Revision,
            ProviderCode = config.ProviderCode, ProviderRateId = observed.ProviderRateId,
            ConnectionKey = observed.ConnectionKey, SourceCurrencyCode = config.SourceCurrencyCode,
            DestinationCurrencyCode = config.DestinationCurrencyCode, ProviderRate = observed.Rate, CustomerRate = customer,
            MarkupPercentage = config.MarkupPercentage, RequestedAtUtc = observed.RequestedAtUtc,
            ReceivedAtUtc = observed.ReceivedAtUtc, ProviderTimestampUtc = observed.ProviderTimestampUtc,
            ProviderRequestLogId = observed.RequestLogId, SendEnabled = observed.SendEnabled, SwapEnabled = observed.SwapEnabled, Purpose = purpose
        };
        _db.FxRateObservations.Add(observation);
        config.LastObservationStartedAtUtc = observed.RequestedAtUtc; config.LastSuccessAtUtc = observed.ReceivedAtUtc;
        config.LastProviderTimestampUtc = observed.ProviderTimestampUtc;
        config.LastProviderRate = observed.Rate; config.LastCustomerRate = customer;
        if (config.LastAttemptAtUtc == null || config.LastAttemptAtUtc <= observed.RequestedAtUtc)
        { config.LastAttemptAtUtc = observed.RequestedAtUtc; config.LastError = null; }
        if (!publish) return (null, observation);
        var existing = await _db.ExchangeRates.Where(x => !x.IsDeleted && x.IsActive &&
            x.SourceCurrencyCode == config.SourceCurrencyCode && x.DestinationCurrencyCode == config.DestinationCurrencyCode).ToListAsync(ct);
        foreach (var old in existing) { old.IsActive = false; old.EffectiveTo = observed.ReceivedAtUtc; }
        var rate = new ExchangeRate
        {
            SourceCurrencyCode = config.SourceCurrencyCode, DestinationCurrencyCode = config.DestinationCurrencyCode,
            ProviderCode = config.ProviderCode, ProviderRateId = observed.ProviderRateId,
            ProviderRate = observed.Rate, CustomerRate = customer, MarkupRate = observed.Rate - customer,
            EffectiveFrom = observed.ReceivedAtUtc, IsActive = true,
            FxProviderRateConfigurationId = config.Id, FxRateObservationId = observation.Id
        };
        _db.ExchangeRates.Add(rate);
        return (rate, observation);
    }

    private async Task<T> WithPairLockAsync<T>(string source, string destination, Func<Task<T>> body, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("FX pricing requires its own short database transaction.");
        var before = _db.ChangeTracker.Entries().ToDictionary(x => x.Entity,
            x => (Entry: x, Current: x.CurrentValues.Clone(), Original: x.OriginalValues.Clone(), State: x.State), ReferenceEqualityComparer.Instance);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("KorridorX.Fx:" + source + "/" + destination)));
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key});", ct);
            var result = await body();
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            foreach (var entry in _db.ChangeTracker.Entries().ToArray())
            {
                if (before.TryGetValue(entry.Entity, out var old))
                {
                    entry.CurrentValues.SetValues(old.Current); entry.OriginalValues.SetValues(old.Original); entry.State = old.State;
                }
                else entry.State = EntityState.Detached;
            }
            throw;
        }
    }

    private IFxRateProvider Provider(string code) => _providers.SingleOrDefault(x => string.Equals(x.ProviderCode, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("Choose an installed FX rate provider.");
    private async Task<FxProviderRateConfiguration> SnapshotAsync(Guid id, CancellationToken ct) =>
        await _db.FxProviderRateConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
        ?? throw new InvalidOperationException("FX provider configuration not found.");

    private async Task<FxProviderRateConfiguration> CurrentAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.FxProviderRateConfigurations.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("FX provider configuration not found.");
        await _db.Entry(row).ReloadAsync(ct);
        if (row.IsDeleted) throw new InvalidOperationException("FX provider configuration not found.");
        return row;
    }

    private static void EnsureRevision(FxProviderRateConfiguration row, long? expected)
    {
        if (expected != row.Revision) throw new InvalidOperationException("This FX configuration changed. Reload the page and try again.");
    }
    private static void EnsureSameSelection(FxProviderRateConfiguration current, FxProviderRateConfiguration snapshot, bool requireActive = true)
    {
        EnsureRevision(current, snapshot.Revision);
        if (current.IsActive != snapshot.IsActive || (requireActive && !current.IsActive))
            throw new InvalidOperationException("The active FX configuration changed. Request a new rate.");
    }
    private void EnsureConnection(FxProviderRateConfiguration config, FxProviderRate observed, bool requireActivated)
    {
        if (Provider(config.ProviderCode).ConnectionKey != observed.ConnectionKey ||
            (requireActivated && config.ActivatedConnectionKey != observed.ConnectionKey))
            throw new InvalidOperationException("The FX provider connection changed. Activate it again before requesting a new quote.");
    }
    private async Task CheckPairAsync(FxProviderRateConfiguration config, CancellationToken ct, bool requireActive = true)
    {
        var pair = await ResolvePairAsync(PairType(config), PairId(config), requireActive, true, ct);
        if (pair.SourceCurrencyCode != config.SourceCurrencyCode || pair.DestinationCurrencyCode != config.DestinationCurrencyCode)
            throw new InvalidOperationException("The configured trading pair changed. Create a new FX configuration from the current pair.");
    }

    private async Task<FxPairOptionDto> ResolvePairAsync(string type, Guid id, bool requireActive, bool lockRow, CancellationToken ct)
    {
        FxPairOptionDto pair;
        if (type == "Instant")
        {
            if (lockRow) await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InstantPairs\" WHERE \"Id\" = {id} FOR SHARE;", ct);
            var row = await _db.InstantPairs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Choose an existing instant trading pair.");
            if (requireActive && row.Status != InstantPairStatus.Active) throw new InvalidOperationException("The selected instant pair is not active.");
            pair = new("Instant", row.Id, row.Code, row.SourceAssetCode, row.DestinationAssetCode, row.Status.ToString());
        }
        else if (type == "Marketplace")
        {
            if (lockRow) await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"MarketplacePairs\" WHERE \"Id\" = {id} FOR SHARE;", ct);
            var row = await _db.MarketplacePairs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Choose an existing marketplace trading pair.");
            if (requireActive && row.Status != MarketplacePairStatus.Active) throw new InvalidOperationException("The selected marketplace pair is not active.");
            pair = new("Marketplace", row.Id, row.Code, row.BaseAssetCode, row.QuoteAssetCode, row.Status.ToString());
        }
        else throw new InvalidOperationException("Choose a pair from Trading & business wallets.");
        if (pair.SourceCurrencyCode == pair.DestinationCurrencyCode ||
            await _db.Assets.CountAsync(x => (x.Code == pair.SourceCurrencyCode || x.Code == pair.DestinationCurrencyCode) && x.IsSupported, ct) != 2)
            throw new InvalidOperationException("Both assets in the selected pair must be supported.");
        return pair;
    }

    private FxProviderRateConfiguration FromRequest(SaveFxProviderConfigurationRequestDto request, FxPairOptionDto pair)
    {
        FxPricing.Validate(request.MarkupPercentage, request.MinimumCustomerRate, request.MaximumCustomerRate);
        var provider = Provider(request.ProviderCode);
        var schedule = FxRateSchedule.Validate(request.Schedule);
        var providerRateId = string.IsNullOrWhiteSpace(request.ProviderRateId) ? null : request.ProviderRateId.Trim();
        if (providerRateId?.Length > 150) throw new InvalidOperationException("Provider rate ID is too long.");
        return new FxProviderRateConfiguration
        {
            InstantPairId = pair.PairType == "Instant" ? pair.PairId : null,
            MarketplacePairId = pair.PairType == "Marketplace" ? pair.PairId : null,
            SourceCurrencyCode = pair.SourceCurrencyCode, DestinationCurrencyCode = pair.DestinationCurrencyCode,
            ProviderCode = provider.ProviderCode, ProviderRateId = providerRateId,
            MarkupPercentage = request.MarkupPercentage, MinimumCustomerRate = request.MinimumCustomerRate, MaximumCustomerRate = request.MaximumCustomerRate,
            ScheduleMode = schedule.Mode, IntervalMinutes = schedule.IntervalMinutes, DailyTimes = string.Join(',', schedule.DailyTimes), TimeZoneId = schedule.TimeZoneId
        };
    }

    private static string PairType(FxProviderRateConfiguration x) => x.InstantPairId.HasValue ? "Instant" : "Marketplace";
    private static Guid PairId(FxProviderRateConfiguration x) => x.InstantPairId ?? x.MarketplacePairId ?? Guid.Empty;
    private static FxProviderConfigurationDto ToDto(FxProviderRateConfiguration x, string? code = null) => new()
    {
        Id = x.Id, PairType = PairType(x), PairId = PairId(x), Code = code ?? x.SourceCurrencyCode + "/" + x.DestinationCurrencyCode,
        SourceCurrencyCode = x.SourceCurrencyCode, DestinationCurrencyCode = x.DestinationCurrencyCode,
        ProviderCode = x.ProviderCode, ProviderRateId = x.ProviderRateId, MarkupPercentage = x.MarkupPercentage,
        MinimumCustomerRate = x.MinimumCustomerRate, MaximumCustomerRate = x.MaximumCustomerRate,
        IsActive = x.IsActive, Revision = x.Revision,
        Schedule = new FxRateScheduleDto { Mode = x.ScheduleMode, IntervalMinutes = x.IntervalMinutes,
            DailyTimes = x.DailyTimes.Split(',', StringSplitOptions.RemoveEmptyEntries), TimeZoneId = x.TimeZoneId },
        LastAttemptAtUtc = x.LastAttemptAtUtc, LastSuccessAtUtc = x.LastSuccessAtUtc, LastProviderTimestampUtc = x.LastProviderTimestampUtc,
        LastError = x.LastError, LastProviderRate = x.LastProviderRate, LastCustomerRate = x.LastCustomerRate, NextRefreshAtUtc = x.NextRefreshAtUtc
    };
    private static FxProviderPreviewDto ToPreview(FxProviderRateConfiguration config, FxProviderRate result)
    {
        var customer = FxPricing.Calculate(result.Rate, config.MarkupPercentage, config.MinimumCustomerRate, config.MaximumCustomerRate);
        return new(config.SourceCurrencyCode, config.DestinationCurrencyCode, config.ProviderCode, result.ProviderRateId,
            result.Rate, customer, result.Rate - customer, result.RequestedAtUtc, result.ReceivedAtUtc,
            result.ProviderTimestampUtc, result.SendEnabled, result.SwapEnabled);
    }
    private void StageAudit(Guid userId, string action, FxProviderConfigurationDto config) =>
        _audit.Stage(new AuditRecordRequest(action, "FX", nameof(FxProviderRateConfiguration), config.Id.ToString(),
            NewValues: config, UserId: userId == Guid.Empty ? null : userId));
}
