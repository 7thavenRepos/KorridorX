using KorridorX.Models.Enums;

namespace KorridorX.Dtos.EmbeddedFinance;

public sealed record EmbeddedFinanceAdminApplicationStatusRequestDto(
    ApiApplicationStatus Status,
    string Reason);

public sealed record EmbeddedFinanceAdminReasonRequestDto(
    string Reason);

public sealed record EmbeddedFinanceAdminWebhookStatusRequestDto(
    bool Enabled,
    string Reason);

public sealed record EmbeddedFinanceAdminCustomerStatusRequestDto(
    BusinessCustomerStatus Status,
    string Reason);

public sealed record EmbeddedFinanceAdminCollectionAccountStatusRequestDto(
    CollectionAccountStatus Status,
    string Reason);

public sealed record EmbeddedFinanceAdminActionResultDto(
    Guid Id,
    string Entity,
    string Status,
    DateTime UpdatedAt);

public sealed record EmbeddedFinanceAdminCollectionAccountActionResultDto(
    Guid Id,
    string Status,
    string FinancialAccountStatus,
    DateTime UpdatedAt);
