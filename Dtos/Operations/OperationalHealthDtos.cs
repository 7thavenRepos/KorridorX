namespace KorridorX.Dtos.Operations;

public record OperationalHealthDto(
    string Status,
    bool DatabaseConnected,
    DateTime CheckedAt,
    NotificationHealthDto Notifications,
    ProviderHealthDto Provider,
    WalletHealthDto BusinessWallets,
    PaymentHealthDto Payments,
    RiskHealthDto Risk,
    ComplianceOperationsHealthDto Compliance,
    SupportOperationsHealthDto Support,
    TreasuryOperationsHealthDto Treasury);

public record NotificationHealthDto(
    int Pending,
    int Retry,
    int Processing,
    int DeadLetter,
    int SentLast24Hours);

public record ProviderHealthDto(
    int FailedRequestsLast24Hours,
    int FailedWebhooks,
    int PendingWebhooks,
    int StaleTransactions);

public record WalletHealthDto(
    int FrozenWallets,
    int InconsistentWallets,
    int ActiveReservations);

public record PaymentHealthDto(
    int FailedCollectionsLast24Hours,
    int FailedPayoutsLast24Hours,
    int PendingRefunds);

public record RiskHealthDto(
    int OpenFlags,
    int BlockingFlags,
    int TransfersOnHold,
    int FailedLoginsLast24Hours);


public record ComplianceOperationsHealthDto(
    int OpenCases,
    int BlockingCases,
    int OverdueCases,
    int ScreeningFailuresLast24Hours,
    int PendingScreeningMatches);

public record SupportOperationsHealthDto(
    int OpenTickets,
    int SlaBreachedTickets,
    int OpenDisputes,
    int OpenInvestigations,
    int OverdueInvestigations);

public record TreasuryOperationsHealthDto(
    int LowLiquidityWallets,
    int StaleProviderWallets,
    int StaleFxRates,
    int SettlementVariances);
