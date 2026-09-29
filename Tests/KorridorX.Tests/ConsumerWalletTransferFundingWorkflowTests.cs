using System.Net;
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

    private async Task<TransferSetup> CreateTransferAsync(HttpClient client, decimal walletBalance)
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
                "US",
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
            new CreateConsumerWalletRequestDto("USD"));
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
                SourceCountryCode = "US",
                DestinationCountryCode = "NG",
                SourceCurrencyCode = "USD",
                DestinationCurrencyCode = "NGN",
                TransferType = TransferType.ConsumerToConsumer,
                SourceAmount = 100m
            });
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quoteEnvelope = await quoteResponse.ReadApiResponseAsync<TransferQuoteDto>();
        var quote = Assert.IsType<TransferQuoteDto>(quoteEnvelope.Data);

        var transferResponse = await client.PostJsonAsync(
            "/api/transfers",
            new CreateTransferRequestDto(
                quote.Id,
                recipient.Id,
                bankAccount.Id,
                null,
                TransferPurpose.FamilySupport,
                "Consumer wallet funding integration test",
                "2468"));

        await transferResponse.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);
        var transferEnvelope = await transferResponse.ReadApiResponseAsync<TransferDetailsDto>();
        var details = Assert.IsType<TransferDetailsDto>(transferEnvelope.Data);

        return new TransferSetup(registration.UserId, walletDto.Id, details.Transfer);
    }

    private sealed record TransferSetup(
        Guid UserId,
        Guid WalletId,
        TransferDto Transfer);
}
