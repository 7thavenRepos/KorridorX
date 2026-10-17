using System.Text.RegularExpressions;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;

namespace KorridorX.Services.Treasury;

public static class HouseAccountProvisioningRules
{
    // Stable system-owned treasury identity. Never use the acting administrator's ID.
    // Existing Treasury/Platform house accounts are reused, not migrated to this identity.
    public static readonly Guid TreasuryOwnerId = new("b530c0a3-80e6-4f52-92f2-9d004930fb1d");
    public const int MaximumBatchSize = 50;

    public static string NormalizeAssetCode(string? code)
    {
        var value = code?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(value, "^[A-Z0-9][A-Z0-9_-]{0,19}$"))
            throw new InvalidOperationException("Select a valid asset from configured master data.");
        return value;
    }

    public static string[] NormalizeAssetCodes(string[]? codes)
    {
        if (codes is null || codes.Length is < 1 or > MaximumBatchSize)
            throw new InvalidOperationException("Select between 1 and 50 configured assets.");
        return codes.Select(NormalizeAssetCode).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    public static string AccountCode(string assetCode) => $"KX-HOUSE-{NormalizeAssetCode(assetCode)}";

    public static FinancialAccount CreateZeroBalanceAccount(string assetCode, Guid actorId)
    {
        if (actorId == Guid.Empty) throw new UnauthorizedAccessException("A valid administrator is required.");
        var code = NormalizeAssetCode(assetCode);
        return new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.Treasury,
            OwnerId = TreasuryOwnerId,
            AccountType = FinancialAccountType.House,
            AssetCode = code,
            AccountCode = AccountCode(code),
            Status = FinancialAccountStatus.Active,
            AvailableBalance = 0m,
            SettledBalance = 0m,
            HeldBalance = 0m,
            CreatedByUserId = actorId
        };
    }
}
