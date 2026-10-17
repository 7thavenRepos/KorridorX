using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Treasury;

// Reject owner, balance and status injection rather than silently accepting unknown fields.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProvisionHouseAccountsRequestDto
{
    [Required, MinLength(1), MaxLength(50)]
    public string[] AssetCodes { get; set; } = [];
}

public sealed record TreasuryHouseAccountDto(
    Guid Id, string AccountCode, string AssetCode,
    FinancialAccountOwnerType OwnerType, FinancialAccountStatus Status,
    decimal SettledBalance, decimal AvailableBalance, decimal HeldBalance);

public sealed record ProvisionHouseAccountsResultDto(
    int CreatedCount, int ExistingCount, IReadOnlyList<TreasuryHouseAccountDto> Accounts);
