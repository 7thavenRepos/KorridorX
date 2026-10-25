namespace KorridorX.Services.WalletWithdrawals;

public interface IWalletWithdrawalExecutionService
{
    Task<int> DispatchBatchAsync(int batchSize, CancellationToken ct = default);
    Task<int> RecoverBatchAsync(int batchSize, CancellationToken ct = default);
    Task<bool> RecoverOneAsync(Guid withdrawalId, CancellationToken ct = default);
    Task RetryKnownRejectedOnceAsync(Guid withdrawalId, Guid adminUserId,
        string expectedReference, string reason, CancellationToken ct = default);
    Task CancelUnsubmittedAsync(Guid withdrawalId, Guid adminUserId, string reason,
        CancellationToken ct = default);
}
