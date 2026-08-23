using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class EmbeddedInboundCollectionWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public EmbeddedInboundCollectionWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Completed_blaaiz_deposit_credits_customer_once_and_emits_one_event()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();

        var setup = await CreateScenarioAsync(db);
        var request = Request(setup, amount: 125.50m);

        Assert.True(await service.TryProcessBlaaizDepositAsync(request));
        db.ChangeTracker.Clear();

        Assert.True(await service.TryProcessBlaaizDepositAsync(request));
        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(125.50m, account.SettledBalance);
        Assert.Equal(125.50m, account.AvailableBalance);
        Assert.Equal(0m, account.HeldBalance);

        Assert.Equal(1, await db.Collections.CountAsync(x =>
            x.ProviderCode == ProviderCode.Blaaiz.ToString() &&
            x.ProviderCollectionId == setup.ProviderTransactionId));

        Assert.Equal(1, await db.ProviderTransactions.CountAsync(x =>
            x.ProviderCode == ProviderCode.Blaaiz &&
            x.ProviderTransactionId == setup.ProviderTransactionId &&
            x.CollectionId != null));

        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x =>
            x.Type == LedgerTransactionType.ExternalCollectionCredit &&
            x.IdempotencyKey == setup.ProviderTransactionId));

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "collection.completed"));
    }

    [DatabaseIntegrationFact]
    public async Task Unknown_provider_account_is_not_attributed_and_moves_no_money()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();

        var setup = await CreateScenarioAsync(db);
        var request = new EmbeddedInboundCollectionRequest(
            setup.ProviderTransactionId,
            $"unknown-provider-reference-{Guid.NewGuid():N}",
            "COMPLETED",
            "CAD",
            100m,
            100m,
            0m,
            "CAD",
            $"unknown-provider-account-{Guid.NewGuid():N}",
            $"unknown-provider-customer-{Guid.NewGuid():N}",
            $"unknown-reference-{Guid.NewGuid():N}",
            $"unknown-account-number-{Guid.NewGuid():N}",
            "{\"source\":\"integration-test\"}",
            DateTime.UtcNow);

        Assert.False(await service.TryProcessBlaaizDepositAsync(request));
        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(0m, account.AvailableBalance);
        Assert.Equal(0m, account.SettledBalance);
        Assert.Equal(0, await db.Collections.CountAsync(x => x.ContextEntityId == setup.BusinessCustomerId));
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x => x.IdempotencyKey == setup.ProviderTransactionId));
    }

    [DatabaseIntegrationFact]
    public async Task Wrong_currency_is_rejected_without_ledger_or_balance_change()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();

        var setup = await CreateScenarioAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryProcessBlaaizDepositAsync(Request(setup, currencyCode: "USD")));

        Assert.Contains("currency mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();
        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(0m, account.AvailableBalance);
        Assert.Equal(0m, account.SettledBalance);
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x => x.IdempotencyKey == setup.ProviderTransactionId));
    }

    [DatabaseIntegrationFact]
    public async Task Suspended_collection_account_is_rejected()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();

        var setup = await CreateScenarioAsync(db);

        var collectionAccount = await db.CollectionAccounts.SingleAsync(x => x.Id == setup.CollectionAccountId);
        collectionAccount.Status = CollectionAccountStatus.Suspended;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryProcessBlaaizDepositAsync(Request(setup)));

        Assert.Contains("collection account is not active", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();
        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(0m, account.AvailableBalance);
        Assert.Equal(0m, account.SettledBalance);
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x => x.IdempotencyKey == setup.ProviderTransactionId));
    }

    [DatabaseIntegrationFact]
    public async Task Frozen_financial_account_is_rejected()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();

        var setup = await CreateScenarioAsync(db);

        var account = await db.FinancialAccounts.SingleAsync(x => x.Id == setup.FinancialAccountId);
        account.Status = FinancialAccountStatus.Frozen;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryProcessBlaaizDepositAsync(Request(setup)));

        Assert.Contains("financial account is not active", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();
        account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(0m, account.AvailableBalance);
        Assert.Equal(0m, account.SettledBalance);
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x => x.IdempotencyKey == setup.ProviderTransactionId));
    }

    [DatabaseIntegrationFact]
    public async Task Reused_provider_transaction_with_changed_amount_is_rejected()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();

        var setup = await CreateScenarioAsync(db);

        Assert.True(await service.TryProcessBlaaizDepositAsync(Request(setup, amount: 50m)));
        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TryProcessBlaaizDepositAsync(Request(setup, amount: 75m)));

        Assert.Contains("different deposit details", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(50m, account.AvailableBalance);
        Assert.Equal(50m, account.SettledBalance);
        Assert.Equal(1, await db.Collections.CountAsync(x => x.ProviderCollectionId == setup.ProviderTransactionId));
        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x => x.IdempotencyKey == setup.ProviderTransactionId));
        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "collection.completed"));
    }

    private async Task<Scenario> CreateScenarioAsync(AppDbContext db)
    {
        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Embedded",
                "Owner",
                $"embedded-owner-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550101",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var businessProfile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Embedded Test {unique}",
            CountryCode = "CA",
            ContactEmail = $"embedded-owner-{unique}@example.test",
            ContactPhone = "+12145550101",
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var businessCustomer = new BusinessCustomer
        {
            BusinessProfile = businessProfile,
            ExternalReference = $"CUS-{unique}",
            DisplayName = $"Customer {unique}",
            Email = $"customer-{unique}@example.test",
            PhoneNumber = "+12145550102",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        var financialAccount = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.BusinessCustomer,
            OwnerId = businessCustomer.Id,
            AccountCode = TestReference("ACC-CAD"),
            AssetCode = "CAD",
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = 0m,
            AvailableBalance = 0m,
            HeldBalance = 0m
        };

        var collectionAccount = new CollectionAccount
        {
            BusinessProfile = businessProfile,
            BusinessCustomer = businessCustomer,
            ExternalReference = $"COLL-{unique}",
            AssetCode = "CAD",
            FinancialAccount = financialAccount,
            Status = CollectionAccountStatus.Active
        };

        var providerAccountId = $"blaaiz-va-{unique}";
        var providerCustomerId = $"blaaiz-customer-{unique}";
        var providerReference = $"blaaiz-ref-{unique}";
        var accountNumber = $"000{Random.Shared.Next(1000000, 9999999)}";

        var mapping = new ProviderAccountMapping
        {
            CollectionAccount = collectionAccount,
            ProviderCode = ProviderCode.Blaaiz.ToString(),
            ProviderCustomerId = providerCustomerId,
            ProviderAccountId = providerAccountId,
            ProviderReference = providerReference,
            AccountNumber = accountNumber,
            AccountName = businessCustomer.DisplayName,
            BankName = "Blaaiz Test Bank",
            Status = ProviderAccountMappingStatus.Active
        };

        db.BusinessProfiles.Add(businessProfile);
        db.BusinessCustomers.Add(businessCustomer);
        db.FinancialAccounts.Add(financialAccount);
        db.CollectionAccounts.Add(collectionAccount);
        db.ProviderAccountMappings.Add(mapping);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Scenario(
            businessProfile.Id,
            businessCustomer.Id,
            financialAccount.Id,
            collectionAccount.Id,
            providerAccountId,
            providerCustomerId,
            providerReference,
            accountNumber,
            $"txn-{unique}");
    }

    private static EmbeddedInboundCollectionRequest Request(
        Scenario setup,
        decimal amount = 100m,
        string currencyCode = "CAD",
        string? providerAccountId = null) =>
        new(
            setup.ProviderTransactionId,
            setup.ProviderReference,
            "COMPLETED",
            currencyCode,
            amount,
            amount,
            0m,
            currencyCode,
            providerAccountId ?? setup.ProviderAccountId,
            setup.ProviderCustomerId,
            setup.ProviderReference,
            setup.AccountNumber,
            "{\"source\":\"integration-test\"}",
            DateTime.UtcNow);

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed record Scenario(
        Guid BusinessProfileId,
        Guid BusinessCustomerId,
        Guid FinancialAccountId,
        Guid CollectionAccountId,
        string ProviderAccountId,
        string ProviderCustomerId,
        string ProviderReference,
        string AccountNumber,
        string ProviderTransactionId);
}
