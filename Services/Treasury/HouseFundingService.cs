using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Audit;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Treasury;

public sealed class HouseFundingService(AppDbContext db, ITreasuryService treasury,
    IProviderWalletResolver connection, IAuditService audit, IDataProtectionProvider protection,
    TimeProvider? clock = null)
{
    private readonly IDataProtector _protector = protection.CreateProtector("KorridorX.HouseCompanyFunding.v2");
    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();
    private string Scope => "CompanyHouseFunding:" + connection.EnvironmentName + ":" + Hash(connection.ConnectionKey)[..24];

    public async Task<IReadOnlyList<HouseFundingPositionDto>> PositionsAsync(CancellationToken ct = default)
    {
        var wallets = await db.ProviderWalletBalances.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.ProviderCode.ToUpper() == "BLAAIZ" &&
                (x.NetworkCode == null || x.NetworkCode == ""))
            .Select(x => new ProviderWalletDto { Id = x.Id, ProviderCode = x.ProviderCode,
                ProviderWalletId = x.ProviderWalletId, CurrencyCode = x.CurrencyCode, Balance = x.Balance,
                IsActive = x.IsActive, LastSyncedAt = x.LastSyncedAt }).ToListAsync(ct);
        return await CalculateAsync(wallets, ct);
    }

    private async Task<List<HouseFundingPositionDto>> CalculateAsync(IReadOnlyList<ProviderWalletDto> wallets, CancellationToken ct)
    {
        var codes = BlaaizCurrencyRequirements.All.Select(x => x.CurrencyCode).ToArray();
        var assets = await db.Assets.AsNoTracking().Where(x => codes.Contains(x.Code) && x.Type == AssetType.Fiat)
            .Select(x => new { x.Code, x.DecimalPlaces, x.IsSupported }).ToListAsync(ct);
        var accounts = await db.FinancialAccounts.AsNoTracking().Where(x => codes.Contains(x.AssetCode)).ToListAsync(ct);
        // Blaaiz is the cash source of truth. Receipt history is reconciled in
        // Provider operations and is not added again as a customer liability.
        var positions = new List<HouseFundingPositionDto>();
        foreach (var asset in assets.OrderBy(x => x.Code))
        {
            var p = new HouseFundingPositionDto { AssetCode = asset.Code, DecimalPlaces = asset.DecimalPlaces };
            var cash = wallets.Where(x => x.IsActive && x.ProviderCode.Equals("BLAAIZ", StringComparison.OrdinalIgnoreCase) &&
                x.CurrencyCode.Equals(asset.Code, StringComparison.OrdinalIgnoreCase)).ToList();
            if (cash.Count == 0) HouseFundingPolicy.Warn(p, "Sync provider wallets to obtain a current Blaaiz balance for this currency.");
            else
            {
                if (cash.Select(x => x.ProviderWalletId).Distinct(StringComparer.Ordinal).Count() != cash.Count)
                    HouseFundingPolicy.Warn(p, "Duplicate provider-wallet snapshots require reconciliation.");
                p.ProviderBalance = cash.GroupBy(x => x.ProviderWalletId, StringComparer.Ordinal).Sum(g => g.First().Balance);
                p.ProviderObservedAt = cash.Min(x => x.LastSyncedAt);
                if (p.ProviderObservedAt is DateTime observed && observed < Now.UtcDateTime.AddMinutes(-5))
                    HouseFundingPolicy.Warn(p, "The provider balance is stale. Sync provider wallets before funding.");
            }
            if (!asset.IsSupported) HouseFundingPolicy.Warn(p, "This currency is disabled in master data.");
            foreach (var account in accounts.Where(x => x.AssetCode == asset.Code)) HouseFundingPolicy.AddAccount(p, account);
            positions.Add(p);
        }
        return positions;
    }

    public async Task<HouseFundingPreviewDto> PreviewAsync(Guid actor, PreviewHouseFundingRequestDto request, CancellationToken ct = default)
    {
        RequireActor(actor);
        var reference = Clean(request.FundingReference, 150, "funding reference");
        var reason = Clean(request.Reason, 800, "funding reason");
        var house = await RequireHouseAsync(request.HouseAccountId, ct);
        var key = FundingKey(house.AssetCode, reference);
        var requestHash = RequestHash(house.Id, house.AssetCode, request.Amount, reference, reason);
        var existing = await db.LedgerTransactions.AsNoTracking().Include(x => x.Postings).SingleOrDefaultAsync(x => x.IdempotencyScope == Scope && x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            ValidateExisting(existing, requestHash);
            return new(house.AssetCode, house.Id, house.AccountCode, existing.Amount, reference, reason,
                new HouseFundingPositionDto { AssetCode = house.AssetCode }, Now, null, true, existing.Reference);
        }
        // Reuse the service behind the existing Blaaiz balance-sync endpoint.
        var wallets = await treasury.SyncProviderWalletsAsync(actor, ct);
        var position = (await CalculateAsync(wallets, ct)).Single(x => x.AssetCode == house.AssetCode);
        HouseFundingPolicy.ValidateAmount(position, request.Amount);
        var issued = Now;
        var confirmation = new Confirmation(actor, house.Id, house.AssetCode, request.Amount, reference, reason,
            requestHash, Fingerprint(position, house), Hash(connection.ConnectionKey), connection.EnvironmentName,
            issued, issued.AddMinutes(10));
        return new(house.AssetCode, house.Id, house.AccountCode, request.Amount, reference, reason, position,
            confirmation.ExpiresAt, _protector.Protect(JsonSerializer.Serialize(confirmation)), false, null);
    }

    public async Task<HouseFundingResultDto> ConfirmAsync(Guid actor, ConfirmHouseFundingRequestDto request, CancellationToken ct = default)
    {
        RequireActor(actor);
        if (!request.ConfirmedCompanyFunds)
            throw new InvalidOperationException("Approve recording this amount as company house funding from the displayed Blaaiz balance after customer wallet liabilities.");
        var proof = ReadConfirmation(actor, request.PreviewToken);
        var key = FundingKey(proof.Asset, proof.FundingReference);
        var existing = await db.LedgerTransactions.AsNoTracking().Include(x => x.Postings).SingleOrDefaultAsync(x => x.IdempotencyScope == Scope && x.IdempotencyKey == key, ct);
        if (existing is not null) { ValidateExisting(existing, proof.RequestHash); return Result(existing, proof.HouseAccountId, true); }
        var wallets = await treasury.SyncProviderWalletsAsync(actor, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Same-asset account locks serialize company allocation with wallet money changes.
            await db.FinancialAccounts.FromSqlInterpolated(
                $"SELECT * FROM \"FinancialAccounts\" WHERE \"AssetCode\" = {proof.Asset} ORDER BY \"Id\" FOR UPDATE").ToListAsync(ct);
            existing = await db.LedgerTransactions.AsNoTracking().Include(x => x.Postings).SingleOrDefaultAsync(x => x.IdempotencyScope == Scope && x.IdempotencyKey == key, ct);
            if (existing is not null)
            { ValidateExisting(existing, proof.RequestHash); await tx.CommitAsync(ct); return Result(existing, proof.HouseAccountId, true); }
            var house = await RequireHouseAsync(proof.HouseAccountId, ct, tracked: true);
            var position = (await CalculateAsync(wallets, ct)).Single(x => x.AssetCode == proof.Asset);
            if (Now >= proof.ExpiresAt || Fingerprint(position, house) != proof.Fingerprint) throw ChangedPreview();
            HouseFundingPolicy.ValidateAmount(position, proof.Amount);
            var before = new { house.SettledBalance, house.AvailableBalance, house.HeldBalance };
            var now = Now.UtcDateTime;
            var ledger = new LedgerTransaction { Reference = "KXHF-" + Guid.NewGuid().ToString("N"),
                AssetCode = proof.Asset, Type = LedgerTransactionType.FundingCredit, Status = LedgerTransactionStatus.Posted,
                Amount = proof.Amount, Description = $"Company funding {proof.FundingReference}: {proof.Reason}",
                IdempotencyScope = Scope, IdempotencyKey = key, IdempotencyRequestHash = proof.RequestHash,
                RelatedEntityType = "CompanyHouseFunding", RelatedEntityId = house.Id,
                ContextEntityType = nameof(FinancialAccount), ContextEntityId = house.Id, PostedAt = now, CreatedByUserId = actor };
            HouseFundingPolicy.StageFunding(house, proof.Amount, ledger, actor, now);
            db.LedgerTransactions.Add(ledger);
            audit.Stage(new AuditRecordRequest("HOUSE_COMPANY_FUNDING_POSTED", "Treasury", nameof(FinancialAccount), house.Id.ToString(),
                OldValues: before, NewValues: new { house.SettledBalance, house.AvailableBalance, house.HeldBalance },
                Metadata: new { proof.FundingReference, proof.Reason, proof.Amount, proof.Asset,
                    position.ProviderBalance, position.CustomerLiabilities, position.CustomerHeldIncluded,
                    position.RecordedCompanyFunds, position.OtherAllocatedFunds, position.TotalRecordedAllocations,
                    PositionBasis = "BLAAIZ_BALANCE_MINUS_CUSTOMER_WALLETS_INCLUDING_HOLDS",
                    Environment = connection.EnvironmentName,
                    LedgerTransactionId = ledger.Id, CompanyOwnershipConfirmed = true }, UserId: actor));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result(ledger, house.Id, false);
        }
        catch
        { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }

    private async Task<FinancialAccount> RequireHouseAsync(Guid id, CancellationToken ct, bool tracked = false)
    {
        var query = db.FinancialAccounts.AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var house = await query.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.Status == FinancialAccountStatus.Active &&
            x.AccountType == FinancialAccountType.House &&
            (x.OwnerType == FinancialAccountOwnerType.Treasury || x.OwnerType == FinancialAccountOwnerType.Platform), ct)
            ?? throw new InvalidOperationException("Select an active company house account.");
        if (BlaaizCurrencyRequirements.Find(house.AssetCode) is null)
            throw new InvalidOperationException("Choose a house account in a supported Blaaiz fiat currency.");
        return house;
    }
    private Confirmation ReadConfirmation(Guid actor, string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 16384) throw ChangedPreview();
            var p = JsonSerializer.Deserialize<Confirmation>(_protector.Unprotect(token));
            if (p is null || p.Actor != actor || p.ExpiresAt <= Now || p.IssuedAt > Now.AddSeconds(30) ||
                p.ExpiresAt != p.IssuedAt.AddMinutes(10) || p.ConnectionHash != Hash(connection.ConnectionKey) ||
                p.Environment != connection.EnvironmentName) throw ChangedPreview();
            return p;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException) { throw ChangedPreview(); }
    }
    private static void ValidateExisting(LedgerTransaction ledger, string hash)
    {
        if (ledger.IsDeleted || ledger.Status != LedgerTransactionStatus.Posted || ledger.ReversedAt is not null || ledger.ReversedByTransactionId is not null)
            throw new InvalidOperationException("This funding reference is already recorded and needs reconciliation review.");
        if (ledger.IdempotencyRequestHash != hash) throw new InvalidOperationException("This funding reference was already used with different details.");
    }
    private static HouseFundingResultDto Result(LedgerTransaction ledger, Guid house, bool replay) =>
        new(ledger.Id, ledger.Reference, ledger.AssetCode, house, ledger.Amount,
            ledger.Postings.FirstOrDefault(x => x.Side == LedgerPostingSide.Credit && x.BalanceBucket == LedgerBalanceBucket.Available)?.AccountBalanceAfter ?? 0m, replay);
    private static string Fingerprint(HouseFundingPositionDto p, FinancialAccount house) => Hash(JsonSerializer.Serialize(new {
        p.AssetCode, Cash = DecimalText(p.ProviderBalance), Customers = DecimalText(p.CustomerLiabilities),
        Holds = DecimalText(p.CustomerHeldIncluded), OtherAllocated = DecimalText(p.OtherAllocatedFunds),
        Allocated = DecimalText(p.RecordedCompanyFunds), house.Id, HouseAssetCode = house.AssetCode, house.OwnerId, house.OwnerType,
        house.Status, Settled = DecimalText(house.SettledBalance), Available = DecimalText(house.AvailableBalance),
        Held = DecimalText(house.HeldBalance) }));
    private static string RequestHash(Guid house, string asset, decimal amount, string reference, string reason) =>
        Hash(JsonSerializer.Serialize(new { house, asset, amount = DecimalText(amount), reference, reason }));
    private static string DecimalText(decimal amount) => amount.ToString("G29", CultureInfo.InvariantCulture);
    private static string FundingKey(string asset, string reference) => Hash(asset + "\n" + reference.ToUpperInvariant());
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Clean(string? value, int max, string label) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max && !value.Contains('\r') && !value.Contains('\n')
            ? value.Trim() : throw new InvalidOperationException("Enter a valid " + label + ".");
    private static void RequireActor(Guid actor) { if (actor == Guid.Empty) throw new UnauthorizedAccessException("An authenticated administrator is required."); }
    private static InvalidOperationException ChangedPreview() => new("The funding preview expired or balances changed. Preview the amount again before confirming.");
    private sealed record Confirmation(Guid Actor, Guid HouseAccountId, string Asset, decimal Amount, string FundingReference,
        string Reason, string RequestHash, string Fingerprint, string ConnectionHash, string Environment,
        DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
}
