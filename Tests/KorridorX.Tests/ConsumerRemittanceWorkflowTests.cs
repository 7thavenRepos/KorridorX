using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Fx;
using KorridorX.Dtos.Recipients;
using KorridorX.Dtos.Transfers;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Tests.Infrastructure;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerRemittanceWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ConsumerRemittanceWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_can_register_create_recipient_quote_transfer_and_cancel()
    {
        using var client = _fixture.CreateClient();
        var email = $"consumer-{Guid.NewGuid():N}@example.test";

        var (registration, authentication) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Kay",
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
            var profile = await db.CustomerProfiles
                .SingleAsync(x => x.UserId == registration.UserId);
            profile.KycStatus = KycStatus.Approved;
            profile.KycApprovedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meEnvelope = await meResponse.ReadApiResponseAsync<CurrentUserDto>();
        var currentUser = Assert.IsType<CurrentUserDto>(meEnvelope.Data);
        Assert.Equal(registration.UserId, currentUser.UserId);
        Assert.Equal(new[] { "Consumer" }, currentUser.Roles);
        Assert.True(currentUser.EmailConfirmed);

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

            var mapping = await db.PayoutDestinationProviderMappings
                .FirstOrDefaultAsync(x =>
                    x.DestinationType == PayoutDestinationType.RecipientBankAccount &&
                    x.DestinationId == bankAccount.Id &&
                    x.ProviderCode == ProviderCode.Blaaiz &&
                    !x.IsDeleted);

            if (mapping is null)
            {
                mapping = new PayoutDestinationProviderMapping
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
                };

                db.PayoutDestinationProviderMappings.Add(mapping);
            }
            else
            {
                mapping.ProviderBankId ??= "999";
                mapping.ProviderPartyId ??= $"test-party-{Guid.NewGuid():N}";
                mapping.ProviderDestinationId ??= $"test-destination-{Guid.NewGuid():N}";
                mapping.IsVerified = true;
                mapping.VerificationAttemptedAt ??= DateTime.UtcNow;
                mapping.VerifiedAt ??= DateTime.UtcNow;
                mapping.ProviderVerifiedAccountName ??= bankAccount.AccountName;
                mapping.ProviderVerificationReference ??= $"test-verification-{Guid.NewGuid():N}";
                mapping.LastVerificationError = null;
                mapping.IsActive = true;
            }

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
        Assert.Equal(100m, quote.SourceAmount);
        Assert.True(quote.DestinationAmount > 0);
        Assert.True(quote.TotalPayableAmount > quote.SourceAmount);

        var transferRequest = new CreateTransferRequestDto(
            quote.Id,
            recipient.Id,
            bankAccount.Id,
            null,
            TransferPurpose.FamilySupport,
            "Release candidate end-to-end test",
            "2468");

        var transferResponse = await client.PostJsonAsync("/api/transfers", transferRequest);
        await transferResponse.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);
        var transferEnvelope = await transferResponse.ReadApiResponseAsync<TransferDetailsDto>();
        var transferDetails = Assert.IsType<TransferDetailsDto>(transferEnvelope.Data);

        Assert.Equal(TransferStatus.PendingPayment, transferDetails.Transfer.Status);
        Assert.Equal(recipient.Id, transferDetails.Transfer.RecipientId);
        Assert.Equal(quote.Id, transferDetails.Transfer.TransferQuoteId);
        Assert.Contains(
            transferDetails.TimelineEvents,
            x => string.Equals(x.EventType, "TRANSFER_CREATED", StringComparison.Ordinal));

        var reuseResponse = await client.PostJsonAsync("/api/transfers", transferRequest);
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);
        var reuseError = await reuseResponse.ReadApiResponseAsync<object>();
        Assert.False(reuseError.Success);
        Assert.Equal("INVALID_OPERATION", reuseError.Error?.Code);

        var listResponse = await client.GetAsync("/api/transfers?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listEnvelope = await listResponse.ReadApiResponseAsync<List<TransferDto>>();
        Assert.Contains(listEnvelope.Data ?? [], x => x.Id == transferDetails.Transfer.Id);

        var cancelResponse = await client.PostJsonAsync(
            $"/api/transfers/{transferDetails.Transfer.Id}/cancel",
            new CancelTransferRequestDto("Release candidate cancellation test"));

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelledEnvelope = await cancelResponse.ReadApiResponseAsync<TransferDetailsDto>();
        var cancelled = Assert.IsType<TransferDetailsDto>(cancelledEnvelope.Data);
        Assert.Equal(TransferStatus.Cancelled, cancelled.Transfer.Status);
        Assert.NotNull(cancelled.Transfer.CancelledAt);
        Assert.Contains(
            cancelled.TimelineEvents,
            x => string.Equals(x.EventType, "TRANSFER_CANCELLED", StringComparison.Ordinal));
    }
}
