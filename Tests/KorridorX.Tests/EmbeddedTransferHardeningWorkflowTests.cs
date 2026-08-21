using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Compliance;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Fx;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Payments;
using KorridorX.Services.References;
using KorridorX.Services.Transfers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class EmbeddedTransferHardeningWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public EmbeddedTransferHardeningWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Exact_idempotent_replay_returns_same_transfer_without_second_reservation_or_dispatch()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, 500m);
        var payout = new CountingFailingPayoutService();
        var service = CreateTransferService(scope, db, setup, payout);

        var request = CreateRequest(setup);
        const string key = "idem-transfer-001";

        var first = await service.CreateTransferAsync(
            setup.BusinessCustomerId,
            setup.CollectionAccountId,
            request,
            key);

        db.ChangeTracker.Clear();

        var second = await service.CreateTransferAsync(
            setup.BusinessCustomerId,
            setup.CollectionAccountId,
            request,
            key);

        Assert.Equal(first.Transfer.Id, second.Transfer.Id);
        Assert.Equal(request.ExternalReference, first.Transfer.ExternalReference);
        Assert.Equal(first.Transfer.Reference, second.Transfer.Reference);
        Assert.NotEqual(first.Transfer.ExternalReference, first.Transfer.Reference);
        Assert.StartsWith("TRF-", first.Transfer.Reference, StringComparison.OrdinalIgnoreCase);
        Assert.False(second.PayoutDispatched);
        Assert.Equal(1, payout.DispatchCount);

        Assert.Equal(1, await db.Transfers.CountAsync(x => x.Id == first.Transfer.Id));
        Assert.Equal(1, await db.FinancialReservations.CountAsync(x =>
            x.RelatedEntityType == nameof(Transfer) &&
            x.RelatedEntityId == first.Transfer.Id));

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);

        Assert.Equal(500m - setup.TotalPayableAmount, account.AvailableBalance);
        Assert.Equal(setup.TotalPayableAmount, account.HeldBalance);
        Assert.Equal(500m, account.SettledBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Reused_idempotency_key_with_changed_request_is_rejected_before_quote_reuse_check()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, 500m);
        var payout = new CountingFailingPayoutService();
        var service = CreateTransferService(scope, db, setup, payout);

        const string key = "idem-transfer-002";
        await service.CreateTransferAsync(
            setup.BusinessCustomerId,
            setup.CollectionAccountId,
            CreateRequest(setup),
            key);

        db.ChangeTracker.Clear();

        var changed = CreateRequest(setup) with { PurposeNote = "changed request body" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTransferAsync(
                setup.BusinessCustomerId,
                setup.CollectionAccountId,
                changed,
                key));

        Assert.Contains("different request", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, payout.DispatchCount);
    }

    [DatabaseIntegrationFact]
    public async Task Reusing_external_reference_with_new_idempotency_key_is_rejected_for_same_customer()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, 500m);
        var payout = new CountingFailingPayoutService();
        var service = CreateTransferService(scope, db, setup, payout);
        var request = CreateRequest(setup);

        await service.CreateTransferAsync(
            setup.BusinessCustomerId,
            setup.CollectionAccountId,
            request,
            "idem-external-ref-001");

        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTransferAsync(
                setup.BusinessCustomerId,
                setup.CollectionAccountId,
                request,
                "idem-external-ref-002"));

        Assert.Contains("External reference", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.Transfers.CountAsync(x =>
            x.BusinessCustomerId == setup.BusinessCustomerId &&
            x.ExternalReference == request.ExternalReference));
    }

    [DatabaseIntegrationFact]
    public async Task Insufficient_balance_creates_no_transfer_reservation_or_idempotency_record()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, 5m);
        var payout = new CountingFailingPayoutService();
        var service = CreateTransferService(scope, db, setup, payout);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTransferAsync(
                setup.BusinessCustomerId,
                setup.CollectionAccountId,
                CreateRequest(setup),
                "idem-transfer-insufficient"));

        Assert.Contains("Insufficient", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, payout.DispatchCount);

        db.ChangeTracker.Clear();

        Assert.Equal(0, await db.Transfers.CountAsync(x =>
            x.BusinessCustomerId == setup.BusinessCustomerId &&
            x.SourceFinancialAccountId == setup.FinancialAccountId));

        Assert.Equal(0, await db.FinancialReservations.CountAsync(x =>
            x.ContextEntityId == setup.BusinessCustomerId));

        Assert.Equal(0, await db.EmbeddedApiIdempotencyRecords.CountAsync(x =>
            x.ApiApplicationId == setup.ApiApplicationId &&
            x.IdempotencyKey == "idem-transfer-insufficient"));
    }

    [DatabaseIntegrationFact]
    public async Task Successful_payout_captures_transfer_reservation_exactly_once()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payoutStatus = scope.ServiceProvider.GetRequiredService<IPayoutStatusService>();

        var setup = await CreateSettlementScenarioAsync(db, 200m);

        var payout = await db.Payouts
            .Include(x => x.Transfer)
            .SingleAsync(x => x.Id == setup.PayoutId);

        var changed = payoutStatus.ApplyTransition(
            payout,
            PayoutStatus.Successful,
            new PayoutStatusTransitionContext(
                "IntegrationTest",
                "Provider completed payout.",
                null));

        Assert.True(changed);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var duplicatePayout = await db.Payouts
            .Include(x => x.Transfer)
            .SingleAsync(x => x.Id == setup.PayoutId);

        var duplicateChanged = payoutStatus.ApplyTransition(
            duplicatePayout,
            PayoutStatus.Successful,
            new PayoutStatusTransitionContext(
                "IntegrationTest",
                "Duplicate completion webhook.",
                null));

        Assert.False(duplicateChanged);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);
        var reservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == setup.ReservationId);
        var transfer = await db.Transfers.AsNoTracking()
            .SingleAsync(x => x.Id == setup.TransferId);

        Assert.Equal(0m, account.HeldBalance);
        Assert.Equal(0m, account.AvailableBalance);
        Assert.Equal(0m, account.SettledBalance);
        Assert.Equal(FinancialReservationStatus.Captured, reservation.Status);
        Assert.Equal(200m, reservation.CapturedAmount);
        Assert.Equal(0m, reservation.ReleasedAmount);
        Assert.Equal(TransferStatus.Completed, transfer.Status);

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "transfer.processing"));

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "transfer.completed"));

        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x =>
            x.Type == LedgerTransactionType.ReservationCapture &&
            x.RelatedEntityType == nameof(Transfer) &&
            x.RelatedEntityId == setup.TransferId));
    }

    [DatabaseIntegrationFact]
    public async Task Failed_payout_releases_transfer_reservation_and_restores_available_balance()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payoutStatus = scope.ServiceProvider.GetRequiredService<IPayoutStatusService>();

        var setup = await CreateSettlementScenarioAsync(db, 200m);

        var payout = await db.Payouts
            .Include(x => x.Transfer)
            .SingleAsync(x => x.Id == setup.PayoutId);

        var changed = payoutStatus.ApplyTransition(
            payout,
            PayoutStatus.Failed,
            new PayoutStatusTransitionContext(
                "IntegrationTest",
                "Provider rejected payout.",
                null));

        Assert.True(changed);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.FinancialAccountId);
        var reservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == setup.ReservationId);
        var transfer = await db.Transfers.AsNoTracking()
            .SingleAsync(x => x.Id == setup.TransferId);

        Assert.Equal(0m, account.HeldBalance);
        Assert.Equal(200m, account.AvailableBalance);
        Assert.Equal(200m, account.SettledBalance);
        Assert.Equal(FinancialReservationStatus.Released, reservation.Status);
        Assert.Equal(0m, reservation.CapturedAmount);
        Assert.Equal(200m, reservation.ReleasedAmount);
        Assert.Equal(TransferStatus.RefundPending, transfer.Status);

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "transfer.refund_pending"));

        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x =>
            x.Type == LedgerTransactionType.ReservationRelease &&
            x.RelatedEntityType == nameof(Transfer) &&
            x.RelatedEntityId == setup.TransferId));
    }

    [DatabaseIntegrationFact]
    public async Task Embedded_transfer_status_lifecycle_webhooks_emit_once_per_real_transition()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transferStatus = scope.ServiceProvider.GetRequiredService<ITransferStatusService>();

        var setup = await CreateSettlementScenarioAsync(db, 150m);

        var transfer = await db.Transfers
            .SingleAsync(x => x.Id == setup.TransferId);

        Assert.True(transferStatus.ApplyTransition(
            transfer,
            TransferStatus.Failed,
            new TransferStatusTransitionContext(
                "IntegrationTest",
                "Synthetic transfer failure.",
                null)));

        Assert.False(transferStatus.ApplyTransition(
            transfer,
            TransferStatus.Failed,
            new TransferStatusTransitionContext(
                "IntegrationTest",
                "Duplicate failure.",
                null)));

        Assert.True(transferStatus.ApplyTransition(
            transfer,
            TransferStatus.RefundPending,
            new TransferStatusTransitionContext(
                "IntegrationTest",
                "Refund required.",
                null)));

        Assert.True(transferStatus.ApplyTransition(
            transfer,
            TransferStatus.Refunded,
            new TransferStatusTransitionContext(
                "IntegrationTest",
                "Refund completed.",
                null)));

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "transfer.failed"));

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "transfer.refund_pending"));

        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x =>
            x.BusinessProfileId == setup.BusinessProfileId &&
            x.EventType == "transfer.refunded"));
    }

    private IEmbeddedFinanceTransferService CreateTransferService(
        AsyncServiceScope scope,
        AppDbContext db,
        Scenario setup,
        CountingFailingPayoutService payout)
    {
        return new EmbeddedFinanceTransferService(
            db,
            new FixedEmbeddedContextAccessor(new EmbeddedFinancePrincipal(
                setup.ApiApplicationId,
                Guid.NewGuid(),
                setup.BusinessProfileId,
                EmbeddedFinanceScope.TransfersRead | EmbeddedFinanceScope.TransfersWrite,
                "test-key")),
            scope.ServiceProvider.GetRequiredService<IFinancialReservationService>(),
            scope.ServiceProvider.GetRequiredService<ITransferStatusService>(),
            new NoOpComplianceLimitService(),
            payout,
            new TestReferenceGenerator(),
            new NoOpWebhookPublisher());
    }

    private async Task<Scenario> CreateScenarioAsync(AppDbContext db, decimal availableBalance)
    {
        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Embedded",
                "TransferOwner",
                $"embedded-transfer-owner-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550111",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Embedded Transfer Test {unique}",
            CountryCode = "CA",
            ContactEmail = $"embedded-transfer-owner-{unique}@example.test",
            ContactPhone = "+12145550111",
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var customer = new BusinessCustomer
        {
            BusinessProfile = profile,
            ExternalReference = $"CUS-{unique}",
            DisplayName = $"Embedded Customer {unique}",
            Email = $"embedded-customer-{unique}@example.test",
            PhoneNumber = "+12145550112",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        var account = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.BusinessCustomer,
            OwnerId = customer.Id,
            AccountCode = TestReference("ACC-CAD"),
            AssetCode = "CAD",
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = availableBalance,
            AvailableBalance = availableBalance,
            HeldBalance = 0m
        };

        var collectionAccount = new CollectionAccount
        {
            BusinessProfile = profile,
            BusinessCustomer = customer,
            ExternalReference = $"COLL-{unique}",
            AssetCode = "CAD",
            FinancialAccount = account,
            Status = CollectionAccountStatus.Active
        };

        var beneficiary = new BusinessBeneficiary
        {
            BusinessProfile = profile,
            BeneficiaryType = BusinessBeneficiaryType.Individual,
            Name = $"Recipient {unique}",
            ContactFirstName = "Recipient",
            ContactLastName = "Test",
            CountryCode = "NG",
            Email = $"recipient-{unique}@example.test",
            PhoneNumber = "+2348012345678",
            IsActive = true
        };

        var bankAccount = new BusinessBeneficiaryBankAccount
        {
            BusinessBeneficiary = beneficiary,
            CountryCode = "NG",
            CurrencyCode = "NGN",
            BankName = "Integration Test Bank",
            AccountName = beneficiary.Name,
            AccountNumber = "0123456789",
            IsActive = true,
            IsVerified = true,
            ProviderBankId = $"bank-{unique}",
            ProviderVerifiedAccountName = beneficiary.Name
        };

        beneficiary.BankAccounts.Add(bankAccount);

        var quote = new TransferQuote
        {
            BusinessProfile = profile,
            BusinessCustomer = customer,
            SourceFinancialAccount = account,
            SourceCountryCode = "CA",
            DestinationCountryCode = "NG",
            SourceCurrencyCode = "CAD",
            DestinationCurrencyCode = "NGN",
            TransferType = TransferType.BusinessToConsumer,
            SourceAmount = 100m,
            DestinationAmount = 100000m,
            ProviderRate = 1000m,
            CustomerRate = 1000m,
            FeeAmount = 5m,
            FeeCurrencyCode = "CAD",
            TotalPayableAmount = 105m,
            ProviderCode = "Blaaiz",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false
        };

        var providerCustomer = new ProviderCustomer
        {
            BusinessProfile = profile,
            BusinessCustomer = customer,
            ProviderCode = ProviderCode.Blaaiz,
            ProviderCustomerId = $"provider-customer-{unique}",
            ProviderStatus = "ACTIVE",
            LastSyncedAt = DateTime.UtcNow
        };

        var providerDestination = new PayoutDestinationProviderMapping
        {
            DestinationType = PayoutDestinationType.BusinessBeneficiaryBankAccount,
            DestinationId = bankAccount.Id,
            ProviderCode = ProviderCode.Blaaiz,
            ProviderBankId = bankAccount.ProviderBankId,
            ProviderPartyId = $"provider-party-{unique}",
            ProviderDestinationId = $"provider-destination-{unique}",
            IsVerified = true,
            ProviderVerifiedAccountName = beneficiary.Name,
            ProviderVerificationReference = $"verified-{unique}",
            VerificationAttemptedAt = DateTime.UtcNow,
            VerifiedAt = DateTime.UtcNow,
            IsActive = true
        };

        var application = new ApiApplication
        {
            BusinessProfile = profile,
            Name = $"Embedded App {unique}",
            Scopes = EmbeddedFinanceScope.TransfersRead | EmbeddedFinanceScope.TransfersWrite,
            Status = ApiApplicationStatus.Active
        };

        db.BusinessProfiles.Add(profile);
        db.BusinessCustomers.Add(customer);
        db.FinancialAccounts.Add(account);
        db.CollectionAccounts.Add(collectionAccount);
        db.BusinessBeneficiaries.Add(beneficiary);
        db.BusinessBeneficiaryBankAccounts.Add(bankAccount);
        db.TransferQuotes.Add(quote);
        db.ProviderCustomers.Add(providerCustomer);
        db.PayoutDestinationProviderMappings.Add(providerDestination);
        db.ApiApplications.Add(application);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Scenario(
            profile.Id,
            customer.Id,
            account.Id,
            collectionAccount.Id,
            beneficiary.Id,
            bankAccount.Id,
            quote.Id,
            application.Id,
            quote.TotalPayableAmount);
    }

    private async Task<SettlementScenario> CreateSettlementScenarioAsync(AppDbContext db, decimal amount)
    {
        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Embedded",
                "SettlementOwner",
                $"embedded-settlement-owner-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550121",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Settlement Test {unique}",
            CountryCode = "CA",
            ContactEmail = $"embedded-settlement-owner-{unique}@example.test",
            ContactPhone = "+12145550121",
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var customer = new BusinessCustomer
        {
            BusinessProfile = profile,
            ExternalReference = $"CUS-{unique}",
            DisplayName = $"Settlement Customer {unique}",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        var account = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.BusinessCustomer,
            OwnerId = customer.Id,
            AccountCode = TestReference("ACC-SETTLE"),
            AssetCode = "CAD",
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = amount,
            AvailableBalance = 0m,
            HeldBalance = amount
        };

        var quote = new TransferQuote
        {
            BusinessProfile = profile,
            BusinessCustomer = customer,
            SourceFinancialAccount = account,
            SourceCountryCode = "CA",
            DestinationCountryCode = "NG",
            SourceCurrencyCode = "CAD",
            DestinationCurrencyCode = "NGN",
            TransferType = TransferType.BusinessToConsumer,
            SourceAmount = amount,
            DestinationAmount = amount * 1000m,
            ProviderRate = 1000m,
            CustomerRate = 1000m,
            FeeAmount = 0m,
            FeeCurrencyCode = "CAD",
            TotalPayableAmount = amount,
            ProviderCode = "Blaaiz",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = true,
            UsedAt = DateTime.UtcNow
        };

        var transfer = new Transfer
        {
            Reference = TestReference("TRF"),
            BusinessProfile = profile,
            BusinessCustomer = customer,
            SourceFinancialAccount = account,
            TransferQuote = quote,
            TransferType = TransferType.BusinessToConsumer,
            Purpose = TransferPurpose.FamilySupport,
            SourceCountryCode = "CA",
            DestinationCountryCode = "NG",
            SourceCurrencyCode = "CAD",
            DestinationCurrencyCode = "NGN",
            SourceAmount = amount,
            DestinationAmount = amount * 1000m,
            FeeAmount = 0m,
            FeeCurrencyCode = "CAD",
            TotalPayableAmount = amount,
            CustomerRate = 1000m,
            ProviderRate = 1000m,
            ProviderCode = "Blaaiz",
            ApprovalStatus = BusinessApprovalStatus.NotRequired,
            RequiredApprovals = 0,
            Status = TransferStatus.PaymentReceived
        };

        var reservation = new FinancialReservation
        {
            FinancialAccount = account,
            Type = FinancialReservationType.Transfer,
            RelatedEntityType = nameof(Transfer),
            RelatedEntityId = transfer.Id,
            ContextEntityType = nameof(BusinessCustomer),
            ContextEntityId = customer.Id,
            Reference = TestReference("KXRES"),
            Amount = amount,
            CapturedAmount = 0m,
            ReleasedAmount = 0m,
            Status = FinancialReservationStatus.Active,
            ReservedAt = DateTime.UtcNow
        };

        var payout = new Payout
        {
            Transfer = transfer,
            Purpose = PaymentOperationPurpose.Remittance,
            Reference = TestReference("PAY"),
            CurrencyCode = "NGN",
            Amount = transfer.DestinationAmount,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = PayoutStatus.Processing,
            ProviderCode = "Blaaiz"
        };

        db.BusinessProfiles.Add(profile);
        db.BusinessCustomers.Add(customer);
        db.FinancialAccounts.Add(account);
        db.TransferQuotes.Add(quote);
        db.Transfers.Add(transfer);
        db.FinancialReservations.Add(reservation);
        db.Payouts.Add(payout);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new SettlementScenario(
            profile.Id,
            account.Id,
            transfer.Id,
            reservation.Id,
            payout.Id);
    }

    private static CreateEmbeddedTransferRequestDto CreateRequest(Scenario setup) =>
        new(
            setup.TransferQuoteId,
            setup.BusinessBeneficiaryId,
            setup.BusinessBeneficiaryBankAccountId,
            TransferPurpose.FamilySupport,
            "integration test",
            TestReference("EXT"));

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 50 ? value : value[..50];
    }

    private sealed record Scenario(
        Guid BusinessProfileId,
        Guid BusinessCustomerId,
        Guid FinancialAccountId,
        Guid CollectionAccountId,
        Guid BusinessBeneficiaryId,
        Guid BusinessBeneficiaryBankAccountId,
        Guid TransferQuoteId,
        Guid ApiApplicationId,
        decimal TotalPayableAmount);

    private sealed record SettlementScenario(
        Guid BusinessProfileId,
        Guid FinancialAccountId,
        Guid TransferId,
        Guid ReservationId,
        Guid PayoutId);

    private sealed class FixedEmbeddedContextAccessor : IEmbeddedFinanceContextAccessor
    {
        private readonly EmbeddedFinancePrincipal _principal;
        public FixedEmbeddedContextAccessor(EmbeddedFinancePrincipal principal) => _principal = principal;
        public EmbeddedFinancePrincipal GetRequiredPrincipal() => _principal;
    }

    private sealed class NoOpComplianceLimitService : IComplianceLimitService
    {
        public Task<ComplianceLimitDecision> EvaluateAsync(Transfer transfer, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task EnsureWithinLimitsAsync(Transfer transfer, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<PagedResult<ComplianceLimitDto>> GetLimitsAsync(
            string? countryCode,
            string? currencyCode,
            int page,
            int pageSize,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ComplianceLimitDto> UpsertAsync(
            Guid userId,
            UpsertComplianceLimitRequestDto request,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingFailingPayoutService : IPayoutService
    {
        public int DispatchCount { get; private set; }

        public Task<PayoutDetailsDto> DispatchForTransferAsync(
            Guid transferId,
            string source,
            Guid? changedByUserId = null,
            CancellationToken ct = default)
        {
            DispatchCount++;
            throw new InvalidOperationException("Synthetic provider dispatch stop for integration test.");
        }

        public Task<int> DispatchPendingAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PayoutDetailsDto> RetryFailedAsync(
            Guid payoutId,
            Guid changedByUserId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PayoutDetailsDto> GetPayoutByIdAsync(
            Guid userId,
            Guid payoutId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PayoutDetailsDto> GetTransferPayoutAsync(
            Guid userId,
            Guid transferId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<PayoutDto>> GetMyPayoutsAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestReferenceGenerator : IReferenceGenerator
    {
        public string GenerateTransferReference() => TestReference("TRF");
        public string GenerateCollectionReference() => TestReference("COL");
        public string GeneratePayoutReference() => TestReference("PAY");
        public string GenerateBusinessBatchReference() => TestReference("BAT");
        public string GenerateSupportTicketReference() => TestReference("SUP");
        public string GenerateDisputeReference() => TestReference("DSP");
        public string GenerateInvestigationReference() => TestReference("INV");
    }

    private sealed class NoOpWebhookPublisher : IEmbeddedWebhookPublisher
    {
        public Task<Guid> PublishAsync(
            Guid businessProfileId,
            string eventType,
            object payload,
            CancellationToken ct = default) =>
            Task.FromResult(Guid.NewGuid());
    }
}
