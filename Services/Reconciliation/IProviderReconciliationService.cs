namespace KorridorX.Services.Reconciliation;

public interface IProviderReconciliationService
{
    Task<ProviderReconciliationResult> ReconcilePendingAsync(
        int batchSize,
        CancellationToken ct = default);

    Task<ProviderReconciliationItemResult> ReconcileOneAsync(
        Guid providerTransactionRowId,
        CancellationToken ct = default);
}

public sealed record ProviderReconciliationResult(
    int Examined,
    int Updated,
    int Failed);


public sealed record ProviderReconciliationItemResult(
    Guid ProviderTransactionRowId,
    bool Updated,
    string ProviderStatus,
    DateTime ReconciledAt);
