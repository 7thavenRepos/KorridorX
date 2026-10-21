using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Fx;

public sealed class AdminTransferFeeService(AppDbContext db, IAuditService audit) : IAdminTransferFeeService
{
    public async Task<TransferFeeOptionsDto> GetOptionsAsync(CancellationToken ct = default)
    {
        var instant = await db.InstantPairs.AsNoTracking().Where(x => !x.IsDeleted)
            .Select(x => new TransferFeePairOptionDto(x.SourceAssetCode, x.DestinationAssetCode, x.Status == InstantPairStatus.Active)).ToListAsync(ct);
        var marketplace = await db.MarketplacePairs.AsNoTracking().Where(x => !x.IsDeleted)
            .Select(x => new TransferFeePairOptionDto(x.BaseAssetCode, x.QuoteAssetCode, x.Status == MarketplacePairStatus.Active)).ToListAsync(ct);
        var countries = await db.CountryAssets.AsNoTracking()
            .Where(x => x.Country.IsSupported && x.Asset.IsSupported)
            .Select(x => new TransferFeeCountryOptionDto(x.CountryCode, x.Country.Name, x.AssetCode,
                x.CanSend && x.Country.IsSendCountry, x.CanReceive && x.Country.IsReceiveCountry,
                x.Asset.IndividualEnabled, x.Asset.BusinessEnabled, x.Asset.DecimalPlaces)).ToListAsync(ct);
        // Fee rules follow supported wallet currencies, not a specific country mapping.
        // Country eligibility remains enforced by the consumer/business quote services.
        var supportedAssets = (await db.Assets.AsNoTracking().Where(x => x.IsSupported)
            .Select(x => x.Code).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var pairs = instant.Concat(marketplace)
            .Where(x => supportedAssets.Contains(x.SourceCurrencyCode) && supportedAssets.Contains(x.DestinationCurrencyCode))
            .GroupBy(x => (x.SourceCurrencyCode, x.DestinationCurrencyCode))
            .Select(x => new TransferFeePairOptionDto(x.Key.SourceCurrencyCode, x.Key.DestinationCurrencyCode, x.Any(p => p.IsActive)))
            .OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.DestinationCurrencyCode).ToArray();
        return new(pairs, countries.OrderBy(x => x.CountryName).ThenBy(x => x.AssetCode).ToArray());
    }

    public async Task<IReadOnlyList<AdminTransferFeeDto>> GetAsync(CancellationToken ct = default) =>
        (await db.TransferFees.AsNoTracking().Where(x => !x.IsDeleted)
            .OrderBy(x => x.SourceCurrencyCode).ThenBy(x => x.DestinationCurrencyCode)
            .ThenBy(x => x.TransferType).ThenBy(x => x.MinAmount).ToListAsync(ct))
            .Select(ToDto).ToArray();

    public Task<AdminTransferFeeDto> SaveAsync(Guid actor, Guid? id, SaveTransferFeeRequestDto request, CancellationToken ct = default) =>
        MutateAsync(async () =>
        {
            var source = Normalize(request.SourceCurrencyCode, 20);
            var destination = Normalize(request.DestinationCurrencyCode, 20);
            var precision = await ValidatePairAsync(source, destination, request.TransferType, false, ct);
            TransferFeePolicy.Validate(request, precision);
            TransferFee? fee = null;
            AdminTransferFeeDto? old = null;
            if (id.HasValue)
            {
                fee = await CurrentAsync(id.Value, ct);
                TransferFeePolicy.RequireRevision(fee, request.Revision);
                if (fee.IsActive) throw new InvalidOperationException("Deactivate this fee rule before editing it, or create a new inactive rule.");
                old = ToDto(fee);
            }
            else if (request.Revision is not null)
                throw new InvalidOperationException("A new fee rule must not contain an existing revision.");
            fee ??= new TransferFee { CreatedByUserId = actor, IsActive = false };
            if (!id.HasValue) db.TransferFees.Add(fee);
            // Legacy schema fields remain for historical audit; new rules are global
            // across eligible countries for this currency direction and transfer type.
            fee.SourceCountryCode = "*"; fee.DestinationCountryCode = "*";
            fee.SourceCurrencyCode = source; fee.DestinationCurrencyCode = destination;
            fee.TransferType = request.TransferType; fee.MinAmount = request.MinAmount; fee.MaxAmount = request.MaxAmount;
            fee.FixedFee = request.FixedFee; fee.PercentageFee = request.PercentageFee;
            // Existing quote handlers add the fee to source amount; another fee currency
            // would mislabel that total. The administrator cannot change this independently.
            fee.FeeCurrencyCode = source;
            fee.LastUpdatedAt = TransferFeePolicy.NextUpdate(fee); fee.LastUpdatedByUserId = actor;
            var result = ToDto(fee);
            audit.Stage(new AuditRecordRequest(id.HasValue ? "TransferFeeUpdated" : "TransferFeeCreated", "FX",
                nameof(TransferFee), fee.Id.ToString(), OldValues: old, NewValues: result, UserId: actor));
            await db.SaveChangesAsync(ct);
            return result;
        }, ct);

    public Task<AdminTransferFeeDto> SetActiveAsync(Guid actor, Guid id, string revision, bool active, CancellationToken ct = default) =>
        MutateAsync(async () =>
        {
            var fee = await CurrentAsync(id, ct);
            TransferFeePolicy.RequireRevision(fee, revision);
            if (fee.IsActive == active) return ToDto(fee);
            if (active)
            {
                var precision = await ValidatePairAsync(
                    fee.SourceCurrencyCode, fee.DestinationCurrencyCode, fee.TransferType, true, ct);
                TransferFeePolicy.Validate(new SaveTransferFeeRequestDto { TransferType = fee.TransferType, MinAmount = fee.MinAmount,
                    MaxAmount = fee.MaxAmount, FixedFee = fee.FixedFee, PercentageFee = fee.PercentageFee }, precision);
                if (fee.FeeCurrencyCode != fee.SourceCurrencyCode)
                    throw new InvalidOperationException("Edit this fee rule so its fee currency matches the source currency before activation.");
                var others = await db.TransferFees.AsNoTracking().Where(x => !x.IsDeleted && x.Id != id && x.IsActive &&
                    x.SourceCurrencyCode == fee.SourceCurrencyCode && x.DestinationCurrencyCode == fee.DestinationCurrencyCode &&
                    x.TransferType == fee.TransferType).ToListAsync(ct);
                if (others.Any(x => TransferFeePolicy.Overlaps(fee.MinAmount, fee.MaxAmount, x.MinAmount, x.MaxAmount)))
                    throw new InvalidOperationException("An active fee rule already covers part of this amount range for this currency direction and transfer type. Deactivate it or use a non-overlapping range.");
            }
            var old = ToDto(fee);
            fee.IsActive = active; fee.LastUpdatedAt = TransferFeePolicy.NextUpdate(fee); fee.LastUpdatedByUserId = actor;
            var result = ToDto(fee);
            audit.Stage(new AuditRecordRequest(active ? "TransferFeeActivated" : "TransferFeeDeactivated", "FX",
                nameof(TransferFee), fee.Id.ToString(), OldValues: old, NewValues: result, UserId: actor));
            await db.SaveChangesAsync(ct);
            return result;
        }, ct);

    private async Task<int> ValidatePairAsync(string source, string destination, TransferType type,
        bool activating, CancellationToken ct)
    {
        if (!Enum.IsDefined(type)) throw new InvalidOperationException("Choose a supported transfer type.");
        var options = await GetOptionsAsync(ct);
        var pair = options.Pairs.SingleOrDefault(x => x.SourceCurrencyCode == source && x.DestinationCurrencyCode == destination)
            ?? throw new InvalidOperationException("Select an existing currency direction from Trading & business wallets.");
        if (activating && !pair.IsActive)
            throw new InvalidOperationException("Enable this currency direction in Trading & business wallets before activating its fee rule.");
        var assets = await db.Assets.AsNoTracking().Where(x => x.IsSupported && (x.Code == source || x.Code == destination))
            .Select(x => new { x.Code, x.IndividualEnabled, x.BusinessEnabled, x.DecimalPlaces }).ToListAsync(ct);
        var from = assets.SingleOrDefault(x => x.Code == source);
        var to = assets.SingleOrDefault(x => x.Code == destination);
        var personal = type == TransferType.ConsumerToConsumer;
        if (from is null || to is null ||
            !(personal ? from.IndividualEnabled && to.IndividualEnabled : from.BusinessEnabled && to.BusinessEnabled))
            throw new InvalidOperationException("Both currencies must be supported for the selected transfer type in Master data.");
        return from.DecimalPlaces;
    }

    private async Task<TransferFee> CurrentAsync(Guid id, CancellationToken ct)
    {
        var fee = await db.TransferFees.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Transfer fee rule not found.");
        await db.Entry(fee).ReloadAsync(ct);
        if (fee.IsDeleted) throw new InvalidOperationException("Transfer fee rule not found.");
        return fee;
    }

    private async Task<T> MutateAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Transfer fee changes require their own short transaction.");
        var before = db.ChangeTracker.Entries().ToDictionary(x => x.Entity,
            x => (Current: x.CurrentValues.Clone(), Original: x.OriginalValues.Clone(), State: x.State), ReferenceEqualityComparer.Instance);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // All fee administration takes the same small lock, including changes of
            // corridor. This makes overlap validation atomic without a schema change.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(68475923018340921);", ct);
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var entry in db.ChangeTracker.Entries().ToArray())
            {
                if (before.TryGetValue(entry.Entity, out var old))
                { entry.CurrentValues.SetValues(old.Current); entry.OriginalValues.SetValues(old.Original); entry.State = old.State; }
                else entry.State = EntityState.Detached;
            }
            throw;
        }
    }

    private static string Normalize(string? value, int maximum)
    {
        var result = value?.Trim().ToUpperInvariant() ?? "";
        if (result.Length == 0 || result.Length > maximum) throw new InvalidOperationException("Select a valid configured country and currency direction.");
        return result;
    }

    private static AdminTransferFeeDto ToDto(TransferFee x) => new(x.Id, x.SourceCountryCode, x.DestinationCountryCode,
        x.SourceCurrencyCode, x.DestinationCurrencyCode, x.TransferType, x.MinAmount, x.MaxAmount, x.FixedFee,
        x.PercentageFee, x.FeeCurrencyCode, x.IsActive, TransferFeePolicy.Revision(x), x.LastUpdatedAt);
}
