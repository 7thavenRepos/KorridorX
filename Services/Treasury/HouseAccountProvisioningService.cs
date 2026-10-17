using System.Data;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Treasury;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Treasury;

public sealed class HouseAccountProvisioningService(AppDbContext db, IAuditService audit)
{
    public async Task<PagedResult<TreasuryHouseAccountDto>> GetAsync(
        string? assetCode, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.FinancialAccounts.AsNoTracking().Where(x => !x.IsDeleted &&
            x.AccountType == FinancialAccountType.House &&
            (x.OwnerType == FinancialAccountOwnerType.Treasury || x.OwnerType == FinancialAccountOwnerType.Platform));
        if (!string.IsNullOrWhiteSpace(assetCode))
        {
            var code = HouseAccountProvisioningRules.NormalizeAssetCode(assetCode);
            query = query.Where(x => x.AssetCode == code);
        }
        return await query.OrderBy(x => x.AssetCode).ThenBy(x => x.AccountCode).ThenBy(x => x.Id)
            .Select(x => new TreasuryHouseAccountDto(x.Id, x.AccountCode, x.AssetCode, x.OwnerType,
                x.Status, x.SettledBalance, x.AvailableBalance, x.HeldBalance))
            .PaginateAsync(Math.Clamp(page, 1, 100000), pageSize, ct);
    }

    public async Task<ProvisionHouseAccountsResultDto> ProvisionAsync(
        Guid actorId, ProvisionHouseAccountsRequestDto request, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty) throw new UnauthorizedAccessException("A valid administrator is required.");
        var codes = HouseAccountProvisioningRules.NormalizeAssetCodes(request.AssetCodes);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Transaction-scoped PostgreSQL lock: concurrent bulk requests re-check committed accounts.
        // Existing unique AccountCode and owner/asset/type indexes remain the final safety boundary.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({1247301641}, {9})", ct);
        var supported = await db.Assets.AsNoTracking().Where(x => codes.Contains(x.Code) && x.IsSupported)
            .Select(x => x.Code).ToListAsync(ct);
        var unavailable = codes.Except(supported, StringComparer.Ordinal).ToArray();
        if (unavailable.Length > 0)
            throw new InvalidOperationException("Assets are missing or unsupported in master data: " + string.Join(", ", unavailable));

        var expectedCodes = codes.Select(HouseAccountProvisioningRules.AccountCode).ToArray();
        // Include deleted rows here. Never resurrect a closed/deleted identity or reuse a reserved code.
        var records = await db.FinancialAccounts.AsNoTracking().Where(x =>
            (codes.Contains(x.AssetCode) && x.AccountType == FinancialAccountType.House &&
                (x.OwnerType == FinancialAccountOwnerType.Treasury || x.OwnerType == FinancialAccountOwnerType.Platform)) ||
            expectedCodes.Contains(x.AccountCode)).ToListAsync(ct);
        var existing = new List<FinancialAccount>();
        var created = new List<FinancialAccount>();
        foreach (var code in codes)
        {
            var candidates = records.Where(x => !x.IsDeleted && x.AssetCode == code &&
                x.AccountType == FinancialAccountType.House &&
                (x.OwnerType == FinancialAccountOwnerType.Treasury || x.OwnerType == FinancialAccountOwnerType.Platform)).ToList();
            if (candidates.Count > 0)
            {
                // Frozen/closed accounts stay frozen/closed, and balances/owners remain unchanged.
                existing.AddRange(candidates);
                continue;
            }
            if (records.Any(x => x.AccountCode == HouseAccountProvisioningRules.AccountCode(code) ||
                (x.AssetCode == code && x.OwnerType == FinancialAccountOwnerType.Treasury &&
                 x.OwnerId == HouseAccountProvisioningRules.TreasuryOwnerId && x.AccountType == FinancialAccountType.House)))
                throw new InvalidOperationException($"House account identity for {code} is reserved or deleted. Ask Treasury to review the existing account; no account was reactivated.");
            created.Add(HouseAccountProvisioningRules.CreateZeroBalanceAccount(code, actorId));
        }
        // All input and identity checks complete before any writes are staged.
        db.FinancialAccounts.AddRange(created);
        foreach (var account in created)
            audit.Stage(new AuditRecordRequest("HouseAccountProvisioned", "Treasury", nameof(FinancialAccount),
                account.Id.ToString(), NewValues: ToDto(account),
                Metadata: new { account.OwnerId, account.AccountType, FundingPerformed = false }, UserId: actorId));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ProvisionHouseAccountsResultDto(created.Count, existing.Count,
            existing.Concat(created).OrderBy(x => x.AssetCode).ThenBy(x => x.AccountCode).Select(ToDto).ToArray());
    }

    private static TreasuryHouseAccountDto ToDto(FinancialAccount x) => new(x.Id, x.AccountCode, x.AssetCode,
        x.OwnerType, x.Status, x.SettledBalance, x.AvailableBalance, x.HeldBalance);
}
