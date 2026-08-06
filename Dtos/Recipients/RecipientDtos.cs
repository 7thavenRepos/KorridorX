namespace KorridorX.Dtos.Recipients;

public record RecipientDto
(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    string? RelationshipToSender,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt,
    List<RecipientBankAccountDto> BankAccounts,
    List<RecipientMobileWalletDto> MobileWallets
);

public record RecipientSummaryDto
(
    Guid Id,
    string FullName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    bool IsActive,
    int BankAccountCount,
    int MobileWalletCount,
    DateTime CreatedAt
);

public record RecipientBankAccountDto
(
    Guid Id,
    Guid RecipientId,
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
    bool IsVerified,
    DateTime? VerifiedAt,
    bool IsDefault,
    bool IsActive
);

public record RecipientMobileWalletDto
(
    Guid Id,
    Guid RecipientId,
    string CountryCode,
    string CurrencyCode,
    string ProviderName,
    string WalletNumber,
    string AccountName,
    bool IsVerified,
    DateTime? VerifiedAt,
    bool IsDefault,
    bool IsActive
);

public record CreateRecipientRequestDto
(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    string? RelationshipToSender
);

public record UpdateRecipientRequestDto
(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? Nickname,
    string CountryCode,
    string? PhoneNumber,
    string? Email,
    string? RelationshipToSender,
    bool IsActive
);

public record AddRecipientBankAccountRequestDto
(
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
    bool IsDefault
);

public record UpdateRecipientBankAccountRequestDto
(
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
    bool IsDefault,
    bool IsActive
);

public record AddRecipientMobileWalletRequestDto
(
    string CountryCode,
    string CurrencyCode,
    string ProviderName,
    string WalletNumber,
    string AccountName,
    bool IsDefault
);

public record UpdateRecipientMobileWalletRequestDto
(
    string CountryCode,
    string CurrencyCode,
    string ProviderName,
    string WalletNumber,
    string AccountName,
    bool IsDefault,
    bool IsActive
);
