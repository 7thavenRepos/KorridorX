using KorridorX.Models.Enums;

namespace KorridorX.Dtos.DigitalAssets;

public sealed record CreateDigitalAssetDepositAddressRequestDto(Guid FinancialAccountId, Guid AssetNetworkId, string ProviderCode);
public sealed record DigitalAssetDepositAddressDto(Guid Id, Guid FinancialAccountId, Guid AssetNetworkId, string AssetCode, string NetworkCode, string ProviderCode, string Address, string? DestinationTag, DigitalAssetAddressStatus Status, DateTime CreatedAt);
public sealed record CreateDigitalAssetDepositIntentRequestDto(Guid FinancialAccountId, Guid AssetNetworkId, decimal Amount, string ProviderCode);
public sealed record DigitalAssetDepositIntentDto(
    Guid Id,
    Guid FinancialAccountId,
    Guid AssetNetworkId,
    string AssetCode,
    string NetworkCode,
    string ProviderCode,
    decimal Amount,
    string? Address,
    string? ProviderCollectionId,
    string? ProviderReference,
    CollectionStatus Status,
    DateTime? ProviderExpiresAt,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? FailureReason);
public sealed record CreateDigitalAssetWithdrawalDestinationRequestDto(Guid AssetNetworkId, string Address, string? DestinationTag, string? Label);
public sealed record DigitalAssetWithdrawalDestinationDto(Guid Id, Guid AssetNetworkId, string AssetCode, string NetworkCode, string Address, string? DestinationTag, string? Label, DigitalAssetDestinationStatus Status, DateTime CreatedAt);
public sealed record CreateDigitalAssetWithdrawalRequestDto(
    Guid FinancialAccountId,
    Guid DestinationId,
    decimal Amount,
    string ProviderCode,
    string? ExternalReference,
    DigitalAssetTravelRuleDataDto? TravelRule = null);

public sealed record DigitalAssetTravelRuleDataDto(
    string? OriginatorVasp,
    string? BeneficiaryVasp,
    string? BeneficiaryName,
    string? PayloadJson);
public sealed record DigitalAssetWithdrawalDto(Guid Id, Guid PayoutId, Guid FinancialAccountId, Guid DestinationId, Guid AssetNetworkId, string AssetCode, string NetworkCode, string ProviderCode, decimal Amount, decimal NetworkFee, decimal TotalDebitAmount, DigitalAssetWithdrawalStatus Status, string? TransactionHash, int Confirmations, int RequiredConfirmations, string? FailureReason, DateTime CreatedAt, DateTime? CompletedAt);

public sealed record DigitalAssetInboundNotification(
    string ProviderCode, string ProviderTransactionId, Guid AssetNetworkId, string AssetCode,
    string ToAddress, string? DestinationTag, string? FromAddress, string? TransactionHash,
    decimal Amount, int Confirmations, long? BlockNumber, string? ProviderReference,
    string? RawPayloadJson, DateTime ObservedAt);

public sealed record DigitalAssetOutboundNotification(
    string ProviderCode, string ProviderTransactionId, string Status, int Confirmations,
    string? TransactionHash, long? BlockNumber, decimal? NetworkFee,
    string? ProviderReference, string? RawPayloadJson, DateTime ObservedAt);
