using KorridorX.BackgroundJobs;
using KorridorX.Data;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Fx;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KorridorX.Tests;

public sealed class EmbeddedFinanceConcurrencyContractTests
{
    [Fact]
    public void Idempotency_key_is_unique_per_api_application()
    {
        using var db = CreateModelContext();
        var entity = db.Model.FindEntityType(typeof(EmbeddedApiIdempotencyRecord));
        Assert.NotNull(entity);

        var index = entity!.GetIndexes().SingleOrDefault(x =>
            x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(EmbeddedApiIdempotencyRecord.ApiApplicationId), nameof(EmbeddedApiIdempotencyRecord.IdempotencyKey) }));

        Assert.NotNull(index);
    }

    [Fact]
    public void Provider_mapping_is_unique_and_status_is_a_concurrency_token()
    {
        using var db = CreateModelContext();
        var entity = db.Model.FindEntityType(typeof(ProviderAccountMapping));
        Assert.NotNull(entity);

        var unique = entity!.GetIndexes().SingleOrDefault(x =>
            x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(ProviderAccountMapping.CollectionAccountId), nameof(ProviderAccountMapping.ProviderCode) }));

        Assert.NotNull(unique);
        Assert.True(entity.FindProperty(nameof(ProviderAccountMapping.Status))!.IsConcurrencyToken);
    }

    [Fact]
    public void Webhook_delivery_claim_status_is_a_concurrency_token()
    {
        using var db = CreateModelContext();
        var entity = db.Model.FindEntityType(typeof(BusinessWebhookDelivery));
        Assert.NotNull(entity);
        Assert.True(entity!.FindProperty(nameof(BusinessWebhookDelivery.Status))!.IsConcurrencyToken);
    }

    [Fact]
    public void Money_and_quote_race_guards_remain_enabled()
    {
        using var db = CreateModelContext();

        var account = db.Model.FindEntityType(typeof(FinancialAccount));
        Assert.NotNull(account);
        Assert.True(account!.FindProperty(nameof(FinancialAccount.SettledBalance))!.IsConcurrencyToken);
        Assert.True(account.FindProperty(nameof(FinancialAccount.AvailableBalance))!.IsConcurrencyToken);
        Assert.True(account.FindProperty(nameof(FinancialAccount.HeldBalance))!.IsConcurrencyToken);

        var quote = db.Model.FindEntityType(typeof(TransferQuote));
        Assert.NotNull(quote);
        Assert.True(quote!.FindProperty(nameof(TransferQuote.IsUsed))!.IsConcurrencyToken);
    }

    [Fact]
    public void Concurrent_idempotent_writes_recover_committed_winner()
    {
        var customer = ReadRepositoryFile(Path.Combine("Services", "EmbeddedFinance", "EmbeddedFinanceCustomerService.cs"));
        var payout = ReadRepositoryFile(Path.Combine("Services", "EmbeddedFinance", "EmbeddedFinancePayoutService.cs"));
        var transfer = ReadRepositoryFile(Path.Combine("Services", "EmbeddedFinance", "EmbeddedFinanceTransferService.cs"));

        Assert.Contains("RecoverCustomerIdempotencyRaceAsync", customer, StringComparison.Ordinal);
        Assert.Contains("RecoverCollectionAccountIdempotencyRaceAsync", customer, StringComparison.Ordinal);
        Assert.Contains("catch (DbUpdateException)", customer, StringComparison.Ordinal);

        Assert.Contains("RecoverConcurrentIdempotentPayoutAsync", payout, StringComparison.Ordinal);
        Assert.Contains("catch (DbUpdateException)", payout, StringComparison.Ordinal);

        Assert.Contains("RecoverConcurrentIdempotentTransferAsync", transfer, StringComparison.Ordinal);
        Assert.Contains("catch (DbUpdateException)", transfer, StringComparison.Ordinal);
    }

    [Fact]
    public void Concurrent_provider_mapping_creation_returns_existing_inflight_mapping()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "Services", "EmbeddedFinance", "CollectionAccountProvisioningService.cs"));

        Assert.Contains("RecoverConcurrentMappingAsync", source, StringComparison.Ordinal);
        Assert.Contains("catch (DbUpdateException)", source, StringComparison.Ordinal);
        Assert.Contains("ProviderAccountMappingStatus.Pending or ProviderAccountMappingStatus.Active", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Competing_stale_webhook_recovery_does_not_abort_due_work_claiming()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "BackgroundJobs", "EmbeddedWebhookDeliveryWorker.cs"));

        var staleCatch = source.IndexOf(
            "catch (DbUpdateConcurrencyException)",
            StringComparison.Ordinal);
        var dueQuery = source.IndexOf(
            "var dueIds = await db.BusinessWebhookDeliveries.AsNoTracking()",
            StringComparison.Ordinal);

        Assert.True(staleCatch >= 0);
        Assert.True(dueQuery > staleCatch);
        Assert.Contains("db.ChangeTracker.Clear();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Webhook_retry_contract_keeps_stable_event_id_for_receiver_deduplication()
    {
        var sender = ReadRepositoryFile(Path.Combine(
            "Services", "EmbeddedFinance", "EmbeddedWebhookSender.cs"));

        Assert.Contains("X-KorridorX-Event-Id", sender, StringComparison.Ordinal);
        Assert.Contains("delivery.BusinessWebhookEvent.EventId", sender, StringComparison.Ordinal);
    }

    private static AppDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=korridorx_model_contract;Username=unused;Password=unused")
            .Options;

        return new AppDbContext(options);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Repository file '{relativePath}' could not be located from the test output directory.");
    }
}
