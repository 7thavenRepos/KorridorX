using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Providers;

public sealed record RetainedBankDepositPreviewRequestDto([param: Required, StringLength(1000)] string Reason);
public sealed record RetainedBankDepositApplyRequestDto([param: Required, StringLength(16384)] string PreviewToken);

public sealed record RetainedBankDepositPreviewDto(
    Guid WebhookEventId, string? ProviderEventId, string ProviderTransactionId, string? ProviderReference,
    string CurrencyCode, decimal Amount, decimal CreditAmount, Guid FinancialAccountId, string WalletLabel,
    string OwnerType, Guid OwnerId, Guid DepositAccountId, string ProviderAccountId,
    string? AccountNumber, string? Iban, string? BankName, string? AccountName,
    bool AlreadyCredited, Guid? CollectionId, string Reason, DateTimeOffset ExpiresAt, string PreviewToken);

public sealed record RetainedBankDepositRecoveryResultDto(
    Guid WebhookEventId, string ProviderTransactionId, string CurrencyCode, decimal Amount, decimal CreditAmount,
    Guid FinancialAccountId, Guid CollectionId, bool AlreadyCredited, WebhookProcessingStatus ProcessingStatus, string Message);
