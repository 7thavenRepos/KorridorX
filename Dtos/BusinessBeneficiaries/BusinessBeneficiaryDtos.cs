using KorridorX.Models.Enums;

namespace KorridorX.Dtos.BusinessBeneficiaries;

public record BusinessBeneficiarySummaryDto(
    Guid Id,
    BusinessBeneficiaryType BeneficiaryType,
    string Name,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    bool IsActive,
    int BankAccountCount,
    int MobileWalletCount,
    DateTime CreatedAt);

public record BusinessBeneficiaryDto(
    Guid Id,
    Guid BusinessProfileId,
    BusinessBeneficiaryType BeneficiaryType,
    string Name,
    string? ContactFirstName,
    string? ContactLastName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    string? RelationshipOrPurpose,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt,
    List<BusinessBeneficiaryBankAccountDto> BankAccounts,
    List<BusinessBeneficiaryMobileWalletDto> MobileWallets);

public record CreateBusinessBeneficiaryRequestDto(
    BusinessBeneficiaryType BeneficiaryType,
    string Name,
    string? ContactFirstName,
    string? ContactLastName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    string? RelationshipOrPurpose);

public record UpdateBusinessBeneficiaryRequestDto(
    BusinessBeneficiaryType BeneficiaryType,
    string Name,
    string? ContactFirstName,
    string? ContactLastName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    string? RelationshipOrPurpose,
    bool IsActive);

public record BusinessBeneficiaryBankAccountDto(
    Guid Id,
    Guid BusinessBeneficiaryId,
    string CountryCode,
    string CurrencyCode,
    string BankName,
    string? BankCode,
    string? BranchCode,
    string AccountName,
    string AccountNumber,
    string? Iban,
    string? SwiftBic,
    string? RoutingNumber,
    string? SortCode,
    string? ProviderBankId,
    string? ProviderVerifiedAccountName,
    bool IsVerified,
    DateTime? VerifiedAt,
    string? LastVerificationError,
    bool IsDefault,
    bool IsActive);

public record AddBusinessBeneficiaryBankAccountRequestDto(
    string CountryCode,
    string CurrencyCode,
    string BankName,
    string? BankCode,
    string? BranchCode,
    string AccountName,
    string AccountNumber,
    string? Iban,
    string? SwiftBic,
    string? RoutingNumber,
    string? SortCode,
    string? ProviderBankId,
    bool IsDefault);

public record UpdateBusinessBeneficiaryBankAccountRequestDto(
    string CountryCode,
    string CurrencyCode,
    string BankName,
    string? BankCode,
    string? BranchCode,
    string AccountName,
    string AccountNumber,
    string? Iban,
    string? SwiftBic,
    string? RoutingNumber,
    string? SortCode,
    string? ProviderBankId,
    bool IsDefault,
    bool IsActive);

public record BusinessBeneficiaryBankVerificationDto(
    Guid BusinessBeneficiaryId,
    Guid BankAccountId,
    string ProviderBankId,
    string BankName,
    string MaskedAccountNumber,
    string SubmittedAccountName,
    string ResolvedAccountName,
    bool IsVerified,
    DateTime VerifiedAt);

public record BusinessBeneficiaryMobileWalletDto(
    Guid Id,
    Guid BusinessBeneficiaryId,
    string CountryCode,
    string CurrencyCode,
    string ProviderName,
    string WalletNumber,
    string AccountName,
    bool IsVerified,
    DateTime? VerifiedAt,
    bool IsDefault,
    bool IsActive);

public record AddBusinessBeneficiaryMobileWalletRequestDto(
    string CountryCode,
    string CurrencyCode,
    string ProviderName,
    string WalletNumber,
    string AccountName,
    bool IsDefault);

public record UpdateBusinessBeneficiaryMobileWalletRequestDto(
    string CountryCode,
    string CurrencyCode,
    string ProviderName,
    string WalletNumber,
    string AccountName,
    bool IsDefault,
    bool IsActive);
