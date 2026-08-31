using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;
using KorridorX.Models.Webhooks;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentCollection = KorridorX.Models.Payments.Collection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ProviderOperationsAdminQueryTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ProviderOperationsAdminQueryTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Webhook_detail_returns_payload_and_processing_attempts()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IProviderOperationsQueryService>();

        var suffix = Guid.NewGuid().ToString("N");

        var webhook = new WebhookEvent
        {
            ProviderCode = ProviderCode.Blaaiz,
            ProviderEventId = $"evt-{suffix}",
            EventType = $"provider.test.{suffix}",
            RawPayloadJson = $$"""{"eventId":"{{suffix}}"}""",
            ProcessingStatus = WebhookProcessingStatus.Failed,
            ErrorMessage = "Synthetic provider operations test failure.",
            ReceivedAt = DateTime.UtcNow
        };

        webhook.Attempts.Add(new WebhookProcessingAttempt
        {
            Status = WebhookProcessingStatus.Failed,
            ErrorMessage = "Attempt failed.",
            StartedAt = DateTime.UtcNow.AddSeconds(-1),
            FinishedAt = DateTime.UtcNow
        });

        db.WebhookEvents.Add(webhook);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await service.GetWebhookEventAsync(webhook.Id);

        Assert.Equal(webhook.Id, result.Event.Id);
        Assert.Equal(webhook.ProviderEventId, result.Event.ProviderEventId);
        Assert.Equal(webhook.RawPayloadJson, result.RawPayloadJson);
        Assert.Single(result.Attempts);
        Assert.Equal(WebhookProcessingStatus.Failed, result.Attempts[0].Status);
    }

    [DatabaseIntegrationFact]
    public async Task Failed_payout_queue_returns_only_failed_remittance_payouts()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IProviderOperationsQueryService>();

        var suffix = Guid.NewGuid().ToString("N");

        var failed = new Payout
        {
            Purpose = PaymentOperationPurpose.Remittance,
            Reference = $"PO-FAILED-{suffix}",
            CurrencyCode = "CAD",
            Amount = 42m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = PayoutStatus.Failed,
            ProviderCode = "Blaaiz",
            FailureReason = $"Synthetic failure {suffix}",
            FailedAt = DateTime.UtcNow
        };

        var successful = new Payout
        {
            Purpose = PaymentOperationPurpose.Remittance,
            Reference = $"PO-SUCCESS-{suffix}",
            CurrencyCode = "CAD",
            Amount = 42m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = PayoutStatus.Successful,
            ProviderCode = "Blaaiz"
        };

        db.Payouts.AddRange(failed, successful);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await service.GetFailedPayoutsAsync(
            suffix,
            page: 1,
            pageSize: 20);

        Assert.Contains(result.Items, x => x.Id == failed.Id);
        Assert.DoesNotContain(result.Items, x => x.Id == successful.Id);
    }

    [DatabaseIntegrationFact]
    public async Task Pending_payout_queue_returns_funded_transfer_without_existing_payout()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IProviderOperationsQueryService>();
        var suffix = Guid.NewGuid().ToString("N");

        var quote = CreateQuote(suffix);
        var transfer = CreateTransfer(quote, $"TRF-PENDING-{suffix}");
        var dispatchedQuote = CreateQuote($"existing-{suffix}");
        var alreadyDispatched = CreateTransfer(dispatchedQuote, $"TRF-DISPATCHED-{suffix}");

        db.TransferQuotes.AddRange(quote, dispatchedQuote);
        db.Transfers.AddRange(transfer, alreadyDispatched);
        db.Payouts.Add(new Payout
        {
            Transfer = alreadyDispatched,
            Purpose = PaymentOperationPurpose.Remittance,
            Reference = $"PO-EXISTING-{suffix}",
            CurrencyCode = "NGN",
            Amount = alreadyDispatched.DestinationAmount,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = PayoutStatus.Pending,
            ProviderCode = "Blaaiz"
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await service.GetPendingPayoutsAsync(suffix, 1, 20);

        var row = Assert.Single(result.Items, x => x.TransferId == transfer.Id);
        Assert.True(row.CanDispatch);
        Assert.DoesNotContain(result.Items, x => x.TransferId == alreadyDispatched.Id);
    }

    [DatabaseIntegrationFact]
    public async Task Refund_queue_reports_provider_eligibility_and_refresh_state()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IProviderOperationsQueryService>();
        var suffix = Guid.NewGuid().ToString("N");

        var eligible = new PaymentCollection
        {
            Purpose = PaymentOperationPurpose.Remittance,
            Reference = $"COL-ELIGIBLE-{suffix}",
            CurrencyCode = "EUR",
            Amount = 100m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = CollectionStatus.Successful,
            ProviderCode = "Blaaiz",
            ProviderCollectionId = $"provider-collection-{suffix}",
            ConfirmedAt = DateTime.UtcNow
        };

        var pending = new PaymentCollection
        {
            Purpose = PaymentOperationPurpose.Remittance,
            Reference = $"COL-PENDING-{suffix}",
            CurrencyCode = "GBP",
            Amount = 50m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = CollectionStatus.RefundPending,
            ProviderCode = "Blaaiz",
            ProviderCollectionId = $"provider-original-{suffix}",
            ProviderRefundId = $"provider-refund-{suffix}",
            ConfirmedAt = DateTime.UtcNow,
            RefundInitiatedAt = DateTime.UtcNow
        };

        db.Collections.AddRange(eligible, pending);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await service.GetRefundOperationsAsync(suffix, null, 1, 20);

        Assert.Contains(result.Items, x => x.CollectionId == eligible.Id && x.CanInitiate && !x.CanRefresh);
        Assert.Contains(result.Items, x => x.CollectionId == pending.Id && !x.CanInitiate && x.CanRefresh);
    }

    private static TransferQuote CreateQuote(string suffix) => new()
    {
        SourceCountryCode = "CA",
        DestinationCountryCode = "NG",
        SourceCurrencyCode = "CAD",
        DestinationCurrencyCode = "NGN",
        TransferType = TransferType.ConsumerToConsumer,
        SourceAmount = 100m,
        DestinationAmount = 100000m,
        ProviderRate = 1000m,
        CustomerRate = 1000m,
        FeeAmount = 5m,
        FeeCurrencyCode = "CAD",
        TotalPayableAmount = 105m,
        ProviderCode = "Blaaiz",
        ProviderQuoteId = $"quote-{suffix}",
        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        IsUsed = true,
        UsedAt = DateTime.UtcNow
    };

    private static Transfer CreateTransfer(TransferQuote quote, string reference) => new()
    {
        Reference = reference,
        TransferQuote = quote,
        TransferType = TransferType.ConsumerToConsumer,
        Purpose = TransferPurpose.FamilySupport,
        SourceCountryCode = "CA",
        DestinationCountryCode = "NG",
        SourceCurrencyCode = "CAD",
        DestinationCurrencyCode = "NGN",
        SourceAmount = 100m,
        DestinationAmount = 100000m,
        FeeAmount = 5m,
        FeeCurrencyCode = "CAD",
        TotalPayableAmount = 105m,
        CustomerRate = 1000m,
        ProviderRate = 1000m,
        ProviderCode = "Blaaiz",
        Status = TransferStatus.PaymentReceived,
        PaymentReceivedAt = DateTime.UtcNow
    };
}
