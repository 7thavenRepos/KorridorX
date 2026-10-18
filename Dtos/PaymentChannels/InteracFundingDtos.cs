namespace KorridorX.Dtos.PaymentChannels;

public sealed record InteracSendingEmailDto(Guid Id, string Email, string Status, bool IsDefault,
    DateTime? VerifiedAt, DateTime? CodeExpiresAt, DateTime? CanResendAt);
public sealed record InteracFundingDto(Guid WalletId, string AssetCode, bool Available, string? ReceivingEmail,
    Guid? RouteId, string RouteStatus, string Message, int MaxSendingEmails, string? DefaultEmail,
    string? DefaultEmailStatus, IReadOnlyList<InteracSendingEmailDto> SendingEmails, string[] Instructions);
public sealed record AddInteracSendingEmailRequest(string Email);
public sealed record VerifyInteracSendingEmailRequest(string Code);
public sealed record RegisterInteracRouteRequest(Guid ProviderWalletConfigurationId, string ProviderRegistrationId,
    string ReceivingEmail, string DisplayName, bool IndividualEnabled, bool BusinessEnabled, string Reason);
public sealed record VerifyInteracRouteRequest(Guid Revision, string Reason);
public sealed record SetInteracRouteStatusRequest(Guid Revision, bool IsEnabled, string Reason);
public sealed record InteracReceivingRouteDto(Guid Id, Guid Revision, Guid ProviderWalletConfigurationId,
    string ProviderWalletId, string ProviderRegistrationId, string ReceivingEmail, string DisplayName,
    string Status, string? ProviderStatus, DateTime? VerifiedAt, bool VerifiedForCurrentConnection,
    string? FailureReason, bool IndividualEnabled, bool BusinessEnabled);
public sealed record InteracDepositDto(Guid Id, string ProviderTransactionId, string? ProviderReference,
    string Status, string? ReviewReason, string? SenderEmail, string? ReceivingEmail, string CurrencyCode,
    decimal? GrossAmount, decimal? NetAmount, decimal? FeeAmount, Guid? FinancialAccountId,
    Guid? CollectionId, DateTime ReceivedAt, DateTime? CreditedAt);
public sealed record InteracDepositNoteDto(Guid Id, string Note, DateTime CreatedAt, Guid CreatedByUserId);
public sealed record AddInteracDepositNoteRequest(string Note);
public sealed record InteracDepositDetailsDto(Guid Id, string ProviderTransactionId, string? ProviderReference,
    string Status, string? ReviewReason, string? SenderEmail, string? ReceivingEmail, string CurrencyCode,
    decimal? GrossAmount, decimal? NetAmount, decimal? FeeAmount, Guid? FinancialAccountId,
    Guid? CollectionId, DateTime ReceivedAt, DateTime? CreditedAt, IReadOnlyList<InteracDepositNoteDto> Notes);
public sealed record InteracDepositPage(IReadOnlyList<InteracDepositDto> Items, int TotalCount, int Page, int PageSize);
