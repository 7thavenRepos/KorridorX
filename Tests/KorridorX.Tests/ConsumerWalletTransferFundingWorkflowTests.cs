using System.Net;
using System.Reflection;
using KorridorX.Configuration;
using KorridorX.Providers.Remittance;
using KorridorX.Services.BusinessFunding;
using KorridorX.Services.Compliance;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Providers;
using KorridorX.Services.References;
using KorridorX.Services.Security;
using KorridorX.Services.Transfers;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Fx;
using KorridorX.Dtos.Payments;
using KorridorX.Dtos.Recipients;
using KorridorX.Dtos.Transfers;
using KorridorX.Dtos.Wallets;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Services.Payments;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerWalletTransferFundingWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ConsumerWalletTransferFundingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Full_wallet_balance_funds_transfer_without_external_collection()
    {
        using var client = _fixture.CreateClient();
        var setup = await CreateTransferAsync(client, 500m);

        Assert.Equal(TransferStatus.PaymentReceived, setup.Transfer.Status);

        var fundingResponse = await client.GetAsync($"/api/transfers/{setup.Transfer.Id}/funding");
        Assert.Equal(HttpStatusCode.OK, fundingResponse.StatusCode);
        var fundingEnvelope = await fundingResponse.ReadApiResponseAsync<ConsumerTransferFundingDto>();
        var funding = Assert.IsType<ConsumerTransferFundingDto>(fundingEnvelope.Data);

        Assert.True(funding.IsFullyFunded);
        Assert.Equal(0m, funding.ExternalFundingRequired);
        Assert.Equal(setup.Transfer.TotalPayableAmount, funding.ReservedAmount);
        Assert.Equal(setup.WalletId, funding.FinancialAccountId);

        var methodsResponse = await client.GetAsync($"/api/transfers/{setup.Transfer.Id}/collection-methods");
        Assert.Equal(HttpStatusCode.BadRequest, methodsResponse.StatusCode);
    }

    [DatabaseIntegrationFact]
    public async Task Partial_wallet_balance_collects_only_deficit_then_credits_and_reserves_once()
    {
        using var client = _fixture.CreateClient();
        var setup = await CreateTransferAsync(client, 40m);

        Assert.Equal(TransferStatus.PendingPayment, setup.Transfer.Status);

        var fundingBeforeResponse = await client.GetAsync($"/api/transfers/{setup.Transfer.Id}/funding");
        Assert.Equal(HttpStatusCode.OK, fundingBeforeResponse.StatusCode);
        var fundingBeforeEnvelope = await fundingBeforeResponse.ReadApiResponseAsync<ConsumerTransferFundingDto>();
        var fundingBefore = Assert.IsType<ConsumerTransferFundingDto>(fundingBeforeEnvelope.Data);

        Assert.Equal(40m, fundingBefore.ReservedAmount);
        Assert.Equal(setup.Transfer.TotalPayableAmount - 40m, fundingBefore.ExternalFundingRequired);
        Assert.False(fundingBefore.IsFullyFunded);

        var collectionResponse = await client.PostJsonAsync(
            $"/api/transfers/{setup.Transfer.Id}/collections",
            new CreateCollectionRequestDto(PaymentMethod.Card));

        Assert.Equal(HttpStatusCode.OK, collectionResponse.StatusCode);
        var collectionEnvelope = await collectionResponse.ReadApiResponseAsync<CollectionDetailsDto>();
        var collectionDetails = Assert.IsType<CollectionDetailsDto>(collectionEnvelope.Data);

        Assert.Equal(fundingBefore.ExternalFundingRequired, collectionDetails.Collection.Amount);
        Assert.Equal(setup.WalletId, collectionDetails.Collection.FinancialAccountId);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var statuses = scope.ServiceProvider.GetRequiredService<ICollectionStatusService>();

            var collection = await db.Collections
                .Include(x => x.Transfer)
                .SingleAsync(x => x.Id == collectionDetails.Collection.Id);

            // This workflow simulates funding without an external provider receipt.
            // Provider-backed completion now requires an immutable confirmed transaction.
            collection.ProviderCode = "Test";

            var initiated = await statuses.ApplyTransitionAsync(
                collection,
                CollectionStatus.Initiated,
                new CollectionStatusTransitionContext(
                    Source: "WalletFundingTest",
                    Reason: "Synthetic local collection initiation."));
            Assert.True(initiated);

            var changed = await statuses.ApplyTransitionAsync(
                collection,
                CollectionStatus.Successful,
                new CollectionStatusTransitionContext(
                    Source: "WalletFundingTest",
                    Reason: "Synthetic local collection success."));
            Assert.True(changed);

            await db.SaveChangesAsync();

            var replayChanged = await statuses.ApplyTransitionAsync(
                collection,
                CollectionStatus.Successful,
                new CollectionStatusTransitionContext(
                    Source: "WalletFundingTest",
                    Reason: "Synthetic duplicate collection success."));
            Assert.False(replayChanged);

            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();

            var wallet = await db.FinancialAccounts
                .AsNoTracking()
                .SingleAsync(x => x.Id == setup.WalletId);
            var reservation = await db.FinancialReservations
                .AsNoTracking()
                .SingleAsync(x =>
                    x.Type == FinancialReservationType.Transfer &&
                    x.RelatedEntityType == nameof(KorridorX.Models.Transfers.Transfer) &&
                    x.RelatedEntityId == setup.Transfer.Id &&
                    !x.IsDeleted);
            var transfer = await db.Transfers
                .AsNoTracking()
                .SingleAsync(x => x.Id == setup.Transfer.Id);

            Assert.Equal(TransferStatus.PaymentReceived, transfer.Status);
            Assert.Equal(setup.Transfer.TotalPayableAmount, reservation.Amount);
            Assert.Equal(FinancialReservationStatus.Active, reservation.Status);
            Assert.Equal(setup.Transfer.TotalPayableAmount, wallet.HeldBalance);
            Assert.Equal(setup.Transfer.TotalPayableAmount, wallet.SettledBalance);
            Assert.Equal(0m, wallet.AvailableBalance);

            Assert.Equal(
                1,
                await db.LedgerTransactions.CountAsync(x =>
                    x.Type == LedgerTransactionType.ExternalCollectionCredit &&
                    x.RelatedEntityType == nameof(Collection) &&
                    x.RelatedEntityId == collection.Id &&
                    !x.IsDeleted));
        }

        var fundingAfterResponse = await client.GetAsync($"/api/transfers/{setup.Transfer.Id}/funding");
        Assert.Equal(HttpStatusCode.OK, fundingAfterResponse.StatusCode);
        var fundingAfterEnvelope = await fundingAfterResponse.ReadApiResponseAsync<ConsumerTransferFundingDto>();
        var fundingAfter = Assert.IsType<ConsumerTransferFundingDto>(fundingAfterEnvelope.Data);

        Assert.True(fundingAfter.IsFullyFunded);
        Assert.Equal(0m, fundingAfter.ExternalFundingRequired);
        Assert.Equal(setup.Transfer.TotalPayableAmount, fundingAfter.ReservedAmount);
    }

    [DatabaseIntegrationFact]
    public async Task Cancelling_partially_wallet_funded_transfer_releases_held_balance()
    {
        using var client = _fixture.CreateClient();
        var setup = await CreateTransferAsync(client, 30m);

        Assert.Equal(TransferStatus.PendingPayment, setup.Transfer.Status);

        var cancelResponse = await client.PostJsonAsync(
            $"/api/transfers/{setup.Transfer.Id}/cancel",
            new CancelTransferRequestDto("Cancel partial wallet funding test"));

        await cancelResponse.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var wallet = await db.FinancialAccounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == setup.WalletId);
        var reservation = await db.FinancialReservations
            .AsNoTracking()
            .SingleAsync(x =>
                x.Type == FinancialReservationType.Transfer &&
                x.RelatedEntityType == nameof(KorridorX.Models.Transfers.Transfer) &&
                x.RelatedEntityId == setup.Transfer.Id &&
                !x.IsDeleted);

        Assert.Equal(30m, wallet.AvailableBalance);
        Assert.Equal(0m, wallet.HeldBalance);
        Assert.Equal(FinancialReservationStatus.Released, reservation.Status);
        Assert.Equal(30m, reservation.ReleasedAmount);
    }

[DatabaseIntegrationFact]
public async Task Cancelling_fully_funded_not_dispatched_transfer_restores_wallet_once()
{
    using var client = _fixture.CreateClient();
    var setup = await CreateTransferAsync(client, 500m);
    Assert.Equal(TransferStatus.PaymentReceived, setup.Transfer.Status);

    using var cancel = await client.PostJsonAsync(
        $"/api/transfers/{setup.Transfer.Id}/cancel",
        new CancelTransferRequestDto("Cancel before processing"));
    await cancel.EnsureSuccessWithBodyAsync();
    var cancelled = await cancel.ReadApiResponseAsync<TransferDetailsDto>();
    Assert.Equal(TransferStatus.Cancelled, Assert.IsType<TransferDetailsDto>(cancelled.Data).Transfer.Status);

    // A repeated cancellation must not release the wallet reservation twice.
    using var repeated = await client.PostJsonAsync(
        $"/api/transfers/{setup.Transfer.Id}/cancel",
        new CancelTransferRequestDto("Repeated cancellation"));
    Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

    await using var scope = _fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var wallet = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.WalletId);
    var reservation = await db.FinancialReservations.AsNoTracking().SingleAsync(x =>
        x.Type == FinancialReservationType.Transfer &&
        x.RelatedEntityType == nameof(KorridorX.Models.Transfers.Transfer) &&
        x.RelatedEntityId == setup.Transfer.Id && !x.IsDeleted);
    Assert.Equal(500m, wallet.SettledBalance);
    Assert.Equal(500m, wallet.AvailableBalance);
    Assert.Equal(0m, wallet.HeldBalance);
    Assert.Equal(FinancialReservationStatus.Released, reservation.Status);
    Assert.Equal(setup.Transfer.TotalPayableAmount, reservation.ReleasedAmount);
    Assert.Equal(0m, reservation.CapturedAmount);
    Assert.Equal(0, await db.Payouts.CountAsync(x => x.TransferId == setup.Transfer.Id));
    Assert.Equal(1, await db.LedgerTransactions.CountAsync(x =>
        x.Type == LedgerTransactionType.ReservationRelease &&
        x.RelatedEntityType == nameof(KorridorX.Models.Transfers.Transfer) &&
        x.RelatedEntityId == setup.Transfer.Id));
}

[DatabaseIntegrationFact]
public async Task Cancellation_is_blocked_once_payout_record_exists_and_hold_is_preserved()
{
    using var client = _fixture.CreateClient();
    var setup = await CreateTransferAsync(client, 500m);
    Assert.Equal(TransferStatus.PaymentReceived, setup.Transfer.Status);
    await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Payouts.Add(new Payout
        {
            TransferId = setup.Transfer.Id,
            Reference = $"KXPO-TEST-{Guid.NewGuid():N}",
            CurrencyCode = setup.Transfer.DestinationCurrencyCode,
            Amount = setup.Transfer.DestinationAmount,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = PayoutStatus.Pending,
            ProviderCode = "Blaaiz"
        });
        await db.SaveChangesAsync();
    }

    using var response = await client.PostJsonAsync(
        $"/api/transfers/{setup.Transfer.Id}/cancel",
        new CancelTransferRequestDto("Attempt late cancellation"));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    await using var verify = _fixture.Factory.Services.CreateAsyncScope();
    var view = verify.ServiceProvider.GetRequiredService<AppDbContext>();
    var transfer = await view.Transfers.AsNoTracking().SingleAsync(x => x.Id == setup.Transfer.Id);
    var wallet = await view.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.WalletId);
    var reservation = await view.FinancialReservations.AsNoTracking().SingleAsync(x =>
        x.Type == FinancialReservationType.Transfer &&
        x.RelatedEntityType == nameof(KorridorX.Models.Transfers.Transfer) &&
        x.RelatedEntityId == setup.Transfer.Id && !x.IsDeleted);
    Assert.Equal(TransferStatus.PaymentReceived, transfer.Status);
    Assert.Equal(setup.Transfer.TotalPayableAmount, wallet.HeldBalance);
    Assert.Equal(500m - setup.Transfer.TotalPayableAmount, wallet.AvailableBalance);
    Assert.Equal(FinancialReservationStatus.Active, reservation.Status);
    Assert.Equal(0m, reservation.ReleasedAmount);
}

    [DatabaseIntegrationFact]
    public async Task Managed_unavailable_NGN_route_is_rejected_before_wallet_reservation()
    {
        using var client = _fixture.CreateClient();
        var setup = await PrepareTransferAsync(client, 500m);
        var channelId = Guid.NewGuid();
        var walletConfigurationId = Guid.NewGuid();
        try
        {
            await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var resolver = scope.ServiceProvider.GetRequiredService<IProviderWalletResolver>();
                db.ProviderWalletConfigurations.Add(new ProviderWalletConfiguration {
                    Id = walletConfigurationId, ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
                    AssetCode = "NGN", NetworkCode = "", DisplayName = "NGN preflight fixture",
                    ProviderWalletId = "preflight-" + Guid.NewGuid().ToString("N"), IsActive = true,
                    PayoutEnabled = true, VerifiedAt = DateTime.UtcNow, VerifiedConnectionKey = resolver.ConnectionKey });
                db.PaymentChannels.Add(new PaymentChannel {
                    Id = channelId, ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
                    ProviderWalletConfigurationId = walletConfigurationId, AssetCode = "NGN", CountryCode = "NG",
                    Direction = PaymentChannelDirection.Withdrawal, CapabilityCode = "bank-payout",
                    DisplayName = "Paused NGN preflight fixture", IndividualEnabled = true,
                    HasBeenActivated = true, Status = PaymentChannelStatus.Paused,
                    ProviderCapabilityConfirmed = true });
                await db.SaveChangesAsync();
            }
            var response = await client.PostJsonAsync("/api/transfers", setup.Request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("KX-PAYOUT-005", await response.Content.ReadAsStringAsync());
            await using var checkScope = _fixture.Factory.Services.CreateAsyncScope();
            var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wallet = await checkDb.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == setup.WalletId);
            Assert.Equal(500m, wallet.AvailableBalance);
            Assert.Equal(0m, wallet.HeldBalance);
            Assert.False(await checkDb.Transfers.AnyAsync(x => x.TransferQuoteId == setup.QuoteId));
            Assert.False(await checkDb.FinancialReservations.AnyAsync(x => x.FinancialAccountId == setup.WalletId));
            Assert.False((await checkDb.TransferQuotes.AsNoTracking().SingleAsync(x => x.Id == setup.QuoteId)).IsUsed);
        }
        finally
        {
            await using var cleanupScope = _fixture.Factory.Services.CreateAsyncScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await cleanup.PaymentChannels.Where(x => x.Id == channelId).ExecuteDeleteAsync();
            await cleanup.ProviderWalletConfigurations.Where(x => x.Id == walletConfigurationId).ExecuteDeleteAsync();
        }
    }

    private async Task<TransferSetup> CreateTransferAsync(HttpClient client, decimal walletBalance,
        string sourceCurrency = "USD", string sourceCountry = "US", decimal sourceAmount = 100m)
    {
        var setup = await PrepareTransferAsync(client, walletBalance, sourceCurrency, sourceCountry, sourceAmount);
        var transferResponse = await client.PostJsonAsync("/api/transfers", setup.Request);
        await transferResponse.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);
        var transferEnvelope = await transferResponse.ReadApiResponseAsync<TransferDetailsDto>();
        var details = Assert.IsType<TransferDetailsDto>(transferEnvelope.Data);
        return new TransferSetup(setup.UserId, setup.WalletId, details.Transfer);
    }

    private async Task<PreparedTransfer> PrepareTransferAsync(HttpClient client, decimal walletBalance,
        string sourceCurrency = "USD", string sourceCountry = "US", decimal sourceAmount = 100m)
    {
        var email = $"wallet-transfer-{Guid.NewGuid():N}@example.test";
        var (registration, authentication) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Wallet",
                "Tester",
                email,
                "ReleaseCandidate!123",
                "+12145550101",
                sourceCountry,
                UserType.Consumer));
        client.UseBearerToken(authentication.AccessToken);

        var pinResponse = await client.PutAsJsonAsync(
            "/api/auth/transaction-pin",
            new SetTransactionPinRequestDto("ReleaseCandidate!123", "2468"));
        Assert.Equal(HttpStatusCode.OK, pinResponse.StatusCode);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await db.CustomerProfiles.SingleAsync(x => x.UserId == registration.UserId);
            profile.KycStatus = KycStatus.Approved;
            profile.KycApprovedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var walletResponse = await client.PostJsonAsync(
            "/api/wallets",
            new CreateConsumerWalletRequestDto(sourceCurrency));
        Assert.Equal(HttpStatusCode.OK, walletResponse.StatusCode);
        var walletEnvelope = await walletResponse.ReadApiResponseAsync<ConsumerWalletDto>();
        var walletDto = Assert.IsType<ConsumerWalletDto>(walletEnvelope.Data);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wallet = await db.FinancialAccounts.SingleAsync(x => x.Id == walletDto.Id);
            wallet.SettledBalance = walletBalance;
            wallet.AvailableBalance = walletBalance;
            wallet.HeldBalance = 0m;
            await db.SaveChangesAsync();
        }

        var recipientResponse = await client.PostJsonAsync(
            "/api/recipients",
            new CreateRecipientRequestDto(
                "Ada",
                "Recipient",
                null,
                "Ada",
                "NG",
                "+2348012345678",
                "ada.recipient@example.test",
                "Family"));
        Assert.Equal(HttpStatusCode.OK, recipientResponse.StatusCode);
        var recipientEnvelope = await recipientResponse.ReadApiResponseAsync<RecipientDto>();
        var recipient = Assert.IsType<RecipientDto>(recipientEnvelope.Data);

        var bankResponse = await client.PostJsonAsync(
            $"/api/recipients/{recipient.Id}/bank-accounts",
            new AddRecipientBankAccountRequestDto(
                "NG",
                "NGN",
                "KorridorX Test Bank",
                "999",
                null,
                "Ada Recipient",
                "0123456789",
                null,
                null,
                null,
                null,
                true));
        Assert.Equal(HttpStatusCode.OK, bankResponse.StatusCode);
        var bankEnvelope = await bankResponse.ReadApiResponseAsync<RecipientBankAccountDto>();
        var bankAccount = Assert.IsType<RecipientBankAccountDto>(bankEnvelope.Data);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PayoutDestinationProviderMappings.Add(new PayoutDestinationProviderMapping
            {
                DestinationType = PayoutDestinationType.RecipientBankAccount,
                DestinationId = bankAccount.Id,
                ProviderCode = ProviderCode.Blaaiz,
                ProviderBankId = "999",
                ProviderPartyId = $"test-party-{Guid.NewGuid():N}",
                ProviderDestinationId = $"test-destination-{Guid.NewGuid():N}",
                IsVerified = true,
                VerificationAttemptedAt = DateTime.UtcNow,
                VerifiedAt = DateTime.UtcNow,
                ProviderVerifiedAccountName = bankAccount.AccountName,
                ProviderVerificationReference = $"test-verification-{Guid.NewGuid():N}",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var quoteResponse = await client.PostJsonAsync(
            "/api/transfer-quotes",
            new CreateTransferQuoteRequestDto
            {
                SourceCountryCode = sourceCountry,
                DestinationCountryCode = "NG",
                SourceCurrencyCode = sourceCurrency,
                DestinationCurrencyCode = "NGN",
                TransferType = TransferType.ConsumerToConsumer,
                SourceAmount = sourceAmount
            });
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quoteEnvelope = await quoteResponse.ReadApiResponseAsync<TransferQuoteDto>();
        var quote = Assert.IsType<TransferQuoteDto>(quoteEnvelope.Data);

        return new PreparedTransfer(registration.UserId, walletDto.Id, quote.Id,
            new CreateTransferRequestDto(quote.Id, recipient.Id, bankAccount.Id, null,
                TransferPurpose.FamilySupport, "Consumer wallet funding integration test", "2468"));
    }


    [DatabaseIntegrationFact]
    public async Task CAD_customer_reservation_settles_once_through_the_NGN_provider_wallet()
    {
        using var client = _fixture.CreateClient();
        var setup = await CreateTransferAsync(client, 500m, "CAD", "CA", 5m);
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = Options.Create(new BlaaizOptions { IsEnabled = true,
            BaseUrl = "https://blaaiz.example.test", ClientId = "destination-wallet-fixture" });
        var environment = new DestinationEnvironment("Destination-" + Guid.NewGuid().ToString("N"));
        var resolver = new ProviderWalletResolver(db, options, environment);
        var wallet = new ProviderWalletConfiguration { ProviderCode = "BLAAIZ", Environment = environment.EnvironmentName,
            ProviderWalletId = "ngn-settlement-" + Guid.NewGuid().ToString("N"), AssetCode = "NGN", NetworkCode = "",
            DisplayName = "NGN settlement fixture", IsActive = true, PayoutEnabled = true,
            VerifiedAt = DateTime.UtcNow, VerifiedConnectionKey = resolver.ConnectionKey };
        db.ProviderWalletConfigurations.Add(wallet);
        await db.SaveChangesAsync();
        var channels = new PaymentChannelService(db, resolver, options, new CollectionPaymentMethodPolicy());
        await channels.SaveAsync(null, Guid.NewGuid(), new(null, wallet.Id, "NGN", "NG",
            PaymentChannelDirection.Withdrawal, "bank-payout", "NGN settlement", "", true, false, true,
            PaymentChannelStatus.Active, 10, 0m, null, "Destination-wallet regression fixture."), default);
        var remittance = DispatchProxy.Create<IRemittanceProvider, DestinationProviderProxy>();
        var provider = (DestinationProviderProxy)(object)remittance;
        var liquidity = new DestinationLiquidity();
        var service = new PayoutService(db,
            scope.ServiceProvider.GetRequiredService<IReferenceGenerator>(),
            scope.ServiceProvider.GetRequiredService<IPayoutStatusService>(),
            remittance, new DestinationCompliance(),
            scope.ServiceProvider.GetRequiredService<ITransferStatusService>(),
            scope.ServiceProvider.GetRequiredService<IBusinessFundingService>(),
            scope.ServiceProvider.GetRequiredService<ITransferRiskService>(),
            scope.ServiceProvider.GetRequiredService<IComplianceScreeningService>(),
            scope.ServiceProvider.GetRequiredService<IOutboundFundsRestrictionService>(),
            resolver, channels, liquidity);
        await service.DispatchForTransferAsync(setup.Transfer.Id, "DestinationWalletFixture", setup.UserId);
        await service.DispatchForTransferAsync(setup.Transfer.Id, "DestinationWalletFixture", setup.UserId);

        Assert.Equal(1, provider.Calls);
        var sent = Assert.IsType<RemittancePayoutRequest>(provider.Request);
        Assert.Equal("NGN", sent.SourceCurrencyCode);
        Assert.Equal("NGN", sent.DestinationCurrencyCode);
        Assert.Equal(wallet.ProviderWalletId, sent.WalletId);
        Assert.Equal(setup.Transfer.DestinationAmount, sent.DestinationAmount);
        Assert.Null(sent.FromAmount);
        Assert.Equal("NGN", liquidity.Currency);
        Assert.Equal(wallet.ProviderWalletId, liquidity.WalletId);
        db.ChangeTracker.Clear();
        var customerWallet = await db.FinancialAccounts.SingleAsync(x => x.Id == setup.WalletId);
        var reservation = await db.FinancialReservations.SingleAsync(x =>
            x.RelatedEntityType == "Transfer" && x.RelatedEntityId == setup.Transfer.Id && !x.IsDeleted);
        var transfer = await db.Transfers.SingleAsync(x => x.Id == setup.Transfer.Id);
        Assert.Equal("CAD", customerWallet.AssetCode);
        Assert.Equal(500m - setup.Transfer.TotalPayableAmount, customerWallet.SettledBalance);
        Assert.Equal(customerWallet.SettledBalance, customerWallet.AvailableBalance);
        Assert.Equal(0m, customerWallet.HeldBalance);
        Assert.Equal(setup.Transfer.TotalPayableAmount, reservation.CapturedAmount);
        Assert.Equal(FinancialReservationStatus.Captured, reservation.Status);
        Assert.Equal("CAD", transfer.SourceCurrencyCode);
        Assert.Equal(setup.Transfer.CustomerRate, transfer.CustomerRate);
        Assert.Equal(TransferStatus.Completed, transfer.Status);
        Assert.Equal(wallet.Id, (await db.ProviderWalletSelections.SingleAsync(x =>
            x.OperationType == "Payout" && x.OperationId == sent.PayoutId)).WalletConfigurationId);
        Assert.Equal(1, await db.PayoutAttempts.CountAsync(x => x.PayoutId == sent.PayoutId));
    }

    public class DestinationProviderProxy : DispatchProxy
    {
        public RemittancePayoutRequest? Request { get; private set; }
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "get_ProviderName") return "Blaaiz";
            if (method?.Name == "get_ProviderCode") return ProviderCode.Blaaiz;
            if (method?.Name == nameof(IRemittanceProvider.InitiatePayoutAsync))
            {
                Calls++;
                Request = (RemittancePayoutRequest)args![0]!;
                return Task.FromResult(new RemittancePayoutResult("settled-" + Guid.NewGuid().ToString("N"),
                    "fixture-reference", "SUCCESSFUL", Request.DestinationCurrencyCode, Request.DestinationAmount,
                    null, null, DateTime.UtcNow, "{}", Guid.NewGuid()));
            }
            throw new NotSupportedException("No external provider operations are allowed in this fixture.");
        }
    }
    private sealed class DestinationCompliance : IComplianceGateService
    {
        public Task<ComplianceGateResult> EnsureCanInitiateMoneyMovementAsync(Guid a, ProviderCode b,
            CancellationToken ct = default) => Task.FromResult(new ComplianceGateResult("fixture-customer", "VERIFIED"));
        public Task<ComplianceGateResult> EnsureBusinessCanInitiateMoneyMovementAsync(Guid a, ProviderCode b,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class DestinationLiquidity : IProviderPayoutLiquidityService
    {
        public string? Currency { get; private set; }
        public string? WalletId { get; private set; }
        public Task EnsureAvailableAsync(string provider, string currency, decimal amount,
            string? walletId = null, CancellationToken ct = default)
        { Currency = currency; WalletId = walletId; return Task.CompletedTask; }
    }
    private sealed class DestinationEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "KorridorX.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record PreparedTransfer(Guid UserId, Guid WalletId, Guid QuoteId, CreateTransferRequestDto Request);

    private sealed record TransferSetup(
        Guid UserId,
        Guid WalletId,
        TransferDto Transfer);
}
