namespace KorridorX.Dtos.Operations;

public record OperationalHealthDto(
    string Status,
    bool DatabaseConnected,
    DateTime CheckedAt,
    NotificationHealthDto Notifications,
    ProviderHealthDto Provider,
    WalletHealthDto BusinessWallets,
    PaymentHealthDto Payments);

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
