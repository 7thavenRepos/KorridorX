using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record CreatePortalCustomerRequestDto(
    [property: Required, MaxLength(150)] string ExternalReference,
    [property: Required, MaxLength(200)] string DisplayName,
    [property: EmailAddress, MaxLength(255)] string? Email,
    [property: MaxLength(50)] string? PhoneNumber,
    [property: Required, MaxLength(10)] string CountryCode);

public sealed record BusinessCollectionCustomerDto(
    Guid Id, string ExternalReference, string DisplayName, string? Email,
    string? PhoneNumber, string CountryCode, BusinessCustomerStatus Status,
    string? ProviderStatus);

public sealed record BusinessCollectionAssetDto(string Code, string Name, bool WalletReady);

public sealed record BusinessCollectionAccountDto(
    Guid Id, string ExternalReference, string AssetCode, CollectionAccountStatus Status,
    decimal SettledBalance, decimal AvailableBalance, decimal HeldBalance,
    ProviderAccountMappingStatus? ProviderStatus, bool IsReady, bool CanRequestBankDetails,
    IReadOnlyList<string> Requirements, string? AccountNumber, string? AccountName,
    string? BankName, VirtualAccountBankDetails? BankDetails, DateTime? LastUpdatedAt);

public sealed record BusinessCollectionOverviewDto(
    BusinessCollectionCustomerDto Customer, bool CanManage, bool BusinessVerified,
    IReadOnlyList<BusinessCollectionAssetDto> SupportedAssets,
    IReadOnlyList<BusinessCollectionAccountDto> Accounts);
