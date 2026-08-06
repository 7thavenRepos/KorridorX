using KorridorX.Models.Enums;

namespace KorridorX.Providers.Remittance;

public sealed record RemittanceProviderCustomerRequest(
    Guid CustomerProfileId,
    string? ExistingProviderCustomerId,
    string FirstName,
    string LastName,
    string Email,
    string CountryCode,
    string? PhoneNumber,
    DateTime? DateOfBirth,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? IdType,
    string? IdNumber,
    DateTime? IdIssueDate,
    DateTime? IdExpiryDate);

public sealed record RemittanceProviderCustomerResult(
    string ProviderCustomerId,
    string ProviderStatus,
    string RawResponseJson,
    Guid ProviderRequestLogId);

public sealed record RemittanceCardDetails(
    string CardHolderName,
    string CardNumber,
    string Expiry,
    string Cvc);

public sealed record RemittanceCollectionRequest(
    Guid TransferId,
    Guid CollectionId,
    PaymentMethod PaymentMethod,
    decimal Amount,
    string CurrencyCode,
    string? ProviderCustomerId,
    string CustomerEmail,
    string CustomerName,
    string? PhoneNumber,
    string? WalletId,
    string? RedirectUrl,
    RemittanceCardDetails? CardDetails,
    int? InteracExpiryHours);

public sealed record RemittanceCollectionResult(
    string ProviderTransactionId,
    string? ProviderReference,
    string ProviderStatus,
    string? CheckoutUrl,
    DateTime? ExpiresAt,
    string RawResponseJson,
    Guid ProviderRequestLogId);

public sealed record RemittanceKycUploadUrlRequest(
    Guid CustomerProfileId,
    string ProviderCustomerId,
    KycDocumentType DocumentType);

public sealed record RemittanceKycUploadUrlResult(
    string ProviderFileId,
    string UploadUrl,
    IReadOnlyDictionary<string, string> UploadHeaders,
    string RawResponseJson,
    Guid ProviderRequestLogId);

public sealed record RemittanceKycDocumentSubmissionRequest(
    Guid CustomerProfileId,
    string ProviderCustomerId,
    string IdentityFileId,
    string? IdentityBackFileId,
    string? ProofOfAddressFileId,
    string? LivenessCheckFileId);

public sealed record RemittanceKycDocumentSubmissionResult(
    string ProviderStatus,
    string RawResponseJson,
    Guid ProviderRequestLogId);


public sealed record RemittancePayoutRequest(
    Guid TransferId,
    Guid PayoutId,
    PaymentMethod PaymentMethod,
    decimal DestinationAmount,
    string SourceCurrencyCode,
    string DestinationCurrencyCode,
    string ProviderCustomerId,
    string WalletId,
    string RecipientFirstName,
    string RecipientLastName,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    string? BankId,
    string? BankName,
    string? AccountName,
    string? AccountNumber,
    string? RoutingNumber,
    string? SortCode,
    string? Iban,
    string? SwiftBic,
    string? Note);

public sealed record RemittancePayoutResult(
    string ProviderTransactionId,
    string? ProviderReference,
    string ProviderStatus,
    string CurrencyCode,
    decimal Amount,
    string? InteracQuestion,
    string? InteracAnswer,
    DateTime? ProviderCreatedAt,
    string RawResponseJson,
    Guid ProviderRequestLogId);

public sealed record RemittanceTransactionStatusResult(
    string ProviderTransactionId,
    string? ProviderReference,
    string TransactionType,
    string ProviderStatus,
    string CurrencyCode,
    decimal Amount,
    string? FailureReason,
    DateTime? ProviderCreatedAt,
    string RawResponseJson,
    Guid ProviderRequestLogId);


public sealed record RemittanceBank(
    string ProviderBankId,
    string Name,
    string Code,
    string? NationalBankCode,
    string? ProviderCountryId,
    string CountryCode,
    string? CountryName,
    string RawResponseJson);

public sealed record RemittanceBankAccountResolutionResult(
    string ProviderBankId,
    string AccountNumber,
    string AccountName,
    string RawResponseJson,
    Guid ProviderRequestLogId);

public sealed record RemittanceWebhookReplayResult(
    string ProviderTransactionId,
    string Message,
    string RawResponseJson,
    Guid ProviderRequestLogId);

public sealed record RemittanceRefundResult(
    string ProviderRefundId,
    string ProviderStatus,
    decimal Amount,
    string CurrencyCode,
    string OriginalTransactionId,
    string? ProviderReference,
    string? ProviderRefundReference,
    string? FailureReason,
    DateTime? ProviderCreatedAt,
    DateTime? ProviderUpdatedAt,
    string RawResponseJson,
    Guid ProviderRequestLogId);
