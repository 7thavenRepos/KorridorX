using System.Reflection;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Payments;
using KorridorX.Services.Reconciliation;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ProviderReconciliationFailureIsolationTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ProviderReconciliationFailureIsolationTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Failed_money_validation_does_not_persist_partial_provider_state_and_batch_continues()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var collectionStatus =
            scope.ServiceProvider.GetRequiredService<ICollectionStatusService>();
        var payoutStatus =
            scope.ServiceProvider.GetRequiredService<IPayoutStatusService>();

        var now = DateTime.UtcNow;

        // The integration database is shared across the RC test collection.
        // Put these rows far ahead of any ordinary stale rows in the
        // reconciliation ordering so batchSize: 2 selects exactly this pair.
        var stale = DateTime.UnixEpoch.AddDays(1);
        var unique = Guid.NewGuid().ToString("N");

        var collection = new Collection
        {
            Purpose = PaymentOperationPurpose.AccountFunding,
            Reference = $"COL-P15B-{unique[..12]}",
            CurrencyCode = "CAD",
            Amount = 100m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = CollectionStatus.Initiated,
            ProviderCode = "Blaaiz",
            ProviderCollectionId = $"bad-{unique}"
        };

        var failedRow = new ProviderTransaction
        {
            ProviderCode = ProviderCode.Blaaiz,
            Collection = collection,
            CollectionId = collection.Id,
            ProviderTransactionId = $"bad-{unique}",
            ProviderReference = "before-reference",
            TransactionType = "collection",
            ProviderStatus = "PROCESSING",
            CurrencyCode = "CAD",
            Amount = 100m,
            RawPayloadJson = """{"state":"before"}""",
            LastSyncedAt = stale
        };

        var successfulRow = new ProviderTransaction
        {
            ProviderCode = ProviderCode.Blaaiz,
            ProviderTransactionId = $"good-{unique}",
            ProviderReference = "before-good-reference",
            TransactionType = "payout",
            ProviderStatus = "PROCESSING",
            CurrencyCode = "CAD",
            Amount = 20m,
            RawPayloadJson = """{"state":"before-good"}""",
            LastSyncedAt = stale.AddSeconds(1)
        };

        db.Collections.Add(collection);
        db.ProviderTransactions.AddRange(
            failedRow,
            successfulRow);
        await db.SaveChangesAsync();

        var failedRowId = failedRow.Id;
        var successfulRowId = successfulRow.Id;
        var collectionId = collection.Id;

        db.ChangeTracker.Clear();

        var provider = CreateProvider(
            new Dictionary<string, Func<RemittanceTransactionStatusResult>>
            {
                [failedRow.ProviderTransactionId] = () =>
                    StatusResult(
                        failedRow.ProviderTransactionId,
                        providerReference: "after-reference-should-not-persist",
                        status: "SUCCESSFUL",
                        currencyCode: "CAD",
                        amount: 90m,
                        raw: """{"state":"bad-result"}"""),
                [successfulRow.ProviderTransactionId] = () =>
                    StatusResult(
                        successfulRow.ProviderTransactionId,
                        providerReference: "good-reference",
                        status: "SUCCESSFUL",
                        currencyCode: "CAD",
                        amount: 20m,
                        raw: """{"state":"good-result"}""")
            });

        var service = new ProviderReconciliationService(
            db,
            provider,
            collectionStatus,
            payoutStatus);

        var result = await service.ReconcilePendingAsync(
            batchSize: 2);

        Assert.Equal(2, result.Examined);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Failed);

        db.ChangeTracker.Clear();

        var storedFailed = await db.ProviderTransactions
            .AsNoTracking()
            .SingleAsync(x => x.Id == failedRowId);

        Assert.Equal("PROCESSING", storedFailed.ProviderStatus);
        Assert.Equal("before-reference", storedFailed.ProviderReference);
        Assert.Equal(100m, storedFailed.Amount);
        Assert.Equal("CAD", storedFailed.CurrencyCode);
        Assert.Equal("""{"state":"before"}""", storedFailed.RawPayloadJson);
        Assert.True(storedFailed.LastSyncedAt > stale);

        var storedCollection = await db.Collections
            .AsNoTracking()
            .SingleAsync(x => x.Id == collectionId);

        Assert.Equal(CollectionStatus.Initiated, storedCollection.Status);
        Assert.Equal(100m, storedCollection.Amount);
        Assert.Equal("CAD", storedCollection.CurrencyCode);
        Assert.Null(storedCollection.ConfirmedAt);
        Assert.Null(storedCollection.FailedAt);

        var storedSuccessful = await db.ProviderTransactions
            .AsNoTracking()
            .SingleAsync(x => x.Id == successfulRowId);

        Assert.Equal("SUCCESSFUL", storedSuccessful.ProviderStatus);
        Assert.Equal("good-reference", storedSuccessful.ProviderReference);
        Assert.Equal(20m, storedSuccessful.Amount);
        Assert.Equal("""{"state":"good-result"}""", storedSuccessful.RawPayloadJson);
        Assert.True(storedSuccessful.LastSyncedAt > stale);
    }

    [DatabaseIntegrationFact]
    public async Task Cancellation_is_propagated_instead_of_recorded_as_failed_reconciliation()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var collectionStatus =
            scope.ServiceProvider.GetRequiredService<ICollectionStatusService>();
        var payoutStatus =
            scope.ServiceProvider.GetRequiredService<IPayoutStatusService>();

        var unique = Guid.NewGuid().ToString("N");

        // Keep this row first in the shared reconciliation queue.
        var stale = DateTime.UnixEpoch.AddDays(2);

        var row = new ProviderTransaction
        {
            ProviderCode = ProviderCode.Blaaiz,
            ProviderTransactionId = $"cancel-{unique}",
            TransactionType = "payout",
            ProviderStatus = "PROCESSING",
            CurrencyCode = "CAD",
            Amount = 10m,
            LastSyncedAt = stale
        };

        db.ProviderTransactions.Add(row);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var provider = CreateProvider(
            new Dictionary<string, Func<RemittanceTransactionStatusResult>>
            {
                [row.ProviderTransactionId] = () =>
                    throw new OperationCanceledException(
                        "Provider request cancelled.")
            });

        var service = new ProviderReconciliationService(
            db,
            provider,
            collectionStatus,
            payoutStatus);

        using var cts = new CancellationTokenSource();

        // The query itself must be allowed to run; cancellation is triggered
        // by the provider call so we exercise the per-item catch behavior.
        SetProviderCancellationHook(provider, cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ReconcilePendingAsync(
                batchSize: 1,
                cts.Token));

        db.ChangeTracker.Clear();

        var stored = await db.ProviderTransactions
            .AsNoTracking()
            .SingleAsync(x => x.Id == row.Id);

        Assert.Equal("PROCESSING", stored.ProviderStatus);

        // PostgreSQL timestamptz/Npgsql can round sub-microsecond ticks.
        // The value must remain operationally unchanged; allow 1 ms storage
        // precision while still proving TouchAsync was not called.
        Assert.InRange(
            stored.LastSyncedAt,
            stale.AddMilliseconds(-1),
            stale.AddMilliseconds(1));
    }

    private static RemittanceTransactionStatusResult StatusResult(
        string transactionId,
        string? providerReference,
        string status,
        string currencyCode,
        decimal amount,
        string raw) =>
        new(
            transactionId,
            providerReference,
            "transaction",
            status,
            currencyCode,
            amount,
            FailureReason: null,
            ProviderCreatedAt: DateTime.UtcNow,
            RawResponseJson: raw,
            ProviderRequestLogId: Guid.NewGuid());

    private static IRemittanceProvider CreateProvider(
        IReadOnlyDictionary<string, Func<RemittanceTransactionStatusResult>> results)
    {
        var provider =
            DispatchProxy.Create<IRemittanceProvider, TestRemittanceProviderProxy>();

        var proxy = (TestRemittanceProviderProxy)(object)provider;
        proxy.Results = results;

        return provider;
    }

    private static void SetProviderCancellationHook(
        IRemittanceProvider provider,
        CancellationTokenSource cts)
    {
        var proxy = (TestRemittanceProviderProxy)(object)provider;
        proxy.BeforeTransactionResult = () => cts.Cancel();
    }

    public class TestRemittanceProviderProxy : DispatchProxy
    {
        public IReadOnlyDictionary<
            string,
            Func<RemittanceTransactionStatusResult>> Results { get; set; } =
            new Dictionary<string, Func<RemittanceTransactionStatusResult>>();

        public Action? BeforeTransactionResult { get; set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException(
                    "Provider proxy method was unavailable.");

            if (targetMethod.Name == "get_ProviderName")
                return "Phase15BProvider";

            if (targetMethod.Name == "get_ProviderCode")
                return ProviderCode.Blaaiz;

            if (targetMethod.Name == nameof(IRemittanceProvider.GetTransactionAsync))
            {
                var transactionId =
                    args?[0] as string
                    ?? throw new InvalidOperationException(
                        "Provider transaction ID was missing.");

                BeforeTransactionResult?.Invoke();

                if (!Results.TryGetValue(
                        transactionId,
                        out var resultFactory))
                {
                    throw new InvalidOperationException(
                        $"No test result configured for {transactionId}.");
                }

                return Task.FromResult(resultFactory());
            }

            throw new NotSupportedException(
                $"The test provider does not implement {targetMethod.Name}.");
        }
    }
}
