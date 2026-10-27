using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KorridorX.Models.AdminFunding;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.AdminFunding;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAdminWalletFundingRequestDto(
    Guid SourceHouseAccountId,
    Guid DestinationWalletId,
    decimal Amount,
    [property: Required, MaxLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewAdminWalletFundingRequestDto(
    [property: MaxLength(1000)] string? Reason);

public sealed record AdminWalletFundingWalletDto(
    Guid WalletId,
    FinancialAccountOwnerType OwnerType,
    Guid OwnerId,
    string OwnerName,
    string? OwnerEmail,
    string AccountCode,
    string AssetCode,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance);

public sealed record AdminWalletFundingHouseAccountDto(
    Guid AccountId,
    string AccountCode,
    string AssetCode,
    FinancialAccountStatus Status,
    decimal SettledBalance,
    decimal AvailableBalance,
    decimal HeldBalance);

public sealed record AdminWalletFundingRequestDto(
    Guid Id,
    string Reference,
    Guid SourceHouseAccountId,
    Guid DestinationWalletId,
    string AssetCode,
    decimal Amount,
    string Reason,
    AdminWalletFundingStatus Status,
    Guid RequestedByUserId,
    DateTime RequestedAtUtc,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    string? RejectionReason,
    Guid? LedgerTransactionId,
    DateTime? PostedAtUtc);
