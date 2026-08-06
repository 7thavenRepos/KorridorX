using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Compliance;

public record StartBusinessKybRequestDto(
    string BusinessName,
    string? TradingName,
    string? BusinessType,
    string RegistrationNumber,
    string? TaxIdentificationNumber,
    string CountryCode,
    DateTime? IncorporationDate,
    string? IndustryType,
    string? BusinessDescription,
    string? Website,
    string? SourceOfFunds,
    string? EstimatedAnnualRevenue,
    int? ExpectedMonthlyPayments,
    string? AccountPurpose,
    BusinessKybScope KybScope,
    string? StateOrProvince,
    string? City,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? OperatingCountryCode,
    string? OperatingStateOrProvince,
    string? OperatingCity,
    string? OperatingAddressLine1,
    string? OperatingAddressLine2,
    string? OperatingPostalCode,
    string ContactEmail,
    string? ContactPhone);

public record UpdateBusinessKybProfileRequestDto(
    string BusinessName,
    string? TradingName,
    string? BusinessType,
    string RegistrationNumber,
    string? TaxIdentificationNumber,
    string CountryCode,
    DateTime? IncorporationDate,
    string? IndustryType,
    string? BusinessDescription,
    string? Website,
    string? SourceOfFunds,
    string? EstimatedAnnualRevenue,
    int? ExpectedMonthlyPayments,
    string? AccountPurpose,
    string? StateOrProvince,
    string? City,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? OperatingCountryCode,
    string? OperatingStateOrProvince,
    string? OperatingCity,
    string? OperatingAddressLine1,
    string? OperatingAddressLine2,
    string? OperatingPostalCode,
    string ContactEmail,
    string? ContactPhone);

public record CreateBusinessOwnerRequestDto(
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    string Nationality,
    string CountryCode,
    string? Title,
    decimal OwnershipPercentage,
    bool HasControl,
    bool IsSigner,
    bool IsBeneficialOwner,
    string IdDocumentType,
    string IdDocumentNumber,
    string IdDocumentCountry,
    DateTime IdExpiryDate,
    bool IsPep);

public record UpdateBusinessOwnerRequestDto(
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    string Nationality,
    string CountryCode,
    string? Title,
    decimal OwnershipPercentage,
    bool HasControl,
    bool IsSigner,
    bool IsBeneficialOwner,
    string IdDocumentType,
    string? IdDocumentNumber,
    string IdDocumentCountry,
    DateTime IdExpiryDate,
    bool IsPep);

public record BusinessOwnerUploadUrlRequestDto(
    BusinessOwnerDocumentSide Side,
    string FileName,
    string MimeType);

public record ConfirmBusinessOwnerUploadRequestDto(
    BusinessOwnerDocumentSide Side,
    string ProviderFileId);

public record BusinessDocumentUploadUrlRequestDto(
    BusinessKybDocumentType DocumentType,
    string Name,
    string FileName,
    string MimeType,
    string? Description);

public record ConfirmBusinessDocumentUploadRequestDto(
    string ProviderFileId);

public record BusinessUploadUrlDto(
    Guid RecordId,
    string ProviderFileId,
    string UploadUrl,
    IReadOnlyDictionary<string, string> UploadHeaders);

public record BusinessOwnerDto(
    Guid Id,
    string? ProviderOwnerId,
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    string Nationality,
    string CountryCode,
    string? Title,
    decimal OwnershipPercentage,
    bool HasControl,
    bool IsSigner,
    bool IsBeneficialOwner,
    string IdDocumentType,
    string IdentityNumberLastFour,
    string IdDocumentCountry,
    DateTime IdExpiryDate,
    bool IsPep,
    bool IsIdentityFrontUploaded,
    bool IsIdentityBackUploaded,
    bool AreIdentityFilesAttachedToProvider,
    string ProviderStatus,
    string? RejectionReason,
    string? ProviderAdminCommentsJson);

public record BusinessKybDocumentDto(
    Guid Id,
    BusinessKybDocumentType DocumentType,
    string Name,
    string? Description,
    string FileName,
    string MimeType,
    string? ProviderFileId,
    string? ProviderDocumentId,
    bool IsUploaded,
    bool IsRegisteredWithProvider,
    string ProviderStatus,
    string? RejectionReason,
    string? ProviderAdminCommentsJson);

public record BusinessProfileKybDto(
    Guid Id,
    string BusinessName,
    string? TradingName,
    string? BusinessType,
    string? RegistrationNumber,
    string? TaxIdentificationNumber,
    string CountryCode,
    DateTime? IncorporationDate,
    string? IndustryType,
    string? BusinessDescription,
    string? Website,
    string? SourceOfFunds,
    string? EstimatedAnnualRevenue,
    int? ExpectedMonthlyPayments,
    string? AccountPurpose,
    BusinessKybScope KybScope,
    string? StateOrProvince,
    string? City,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? OperatingCountryCode,
    string? OperatingStateOrProvince,
    string? OperatingCity,
    string? OperatingAddressLine1,
    string? OperatingAddressLine2,
    string? OperatingPostalCode,
    string? ContactEmail,
    string? ContactPhone,
    KybStatus KybStatus,
    DateTime? KybSubmittedAt,
    DateTime? KybApprovedAt,
    DateTime? KybRejectedAt,
    string? KybRejectionReason,
    string? BlaaizBusinessCustomerId);

public record BusinessKybApplicationDto(
    Guid Id,
    Guid BusinessProfileId,
    KybStatus Status,
    BusinessKybScope KybScope,
    string? ProviderApplicationId,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNote,
    DateTime CreatedAt,
    BusinessProfileKybDto Business,
    List<BusinessOwnerDto> Owners,
    List<BusinessKybDocumentDto> Documents);

public record BusinessKybStatusDto(
    BusinessProfileKybDto Business,
    BusinessKybApplicationDto? CurrentApplication);

public record AdminBusinessKybApplicationListItemDto(
    Guid Id,
    Guid BusinessProfileId,
    string BusinessName,
    string? RegistrationNumber,
    string CountryCode,
    BusinessKybScope KybScope,
    KybStatus Status,
    string? ProviderApplicationId,
    int OwnerCount,
    int DocumentCount,
    DateTime? SubmittedAt,
    DateTime CreatedAt);

public record AdminBusinessKybApplicationDetailsDto(
    BusinessKybApplicationDto Application,
    string OwnerUserEmail);
