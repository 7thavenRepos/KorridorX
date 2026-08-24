using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Webhooks;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}