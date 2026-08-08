using System.Net;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Fx;
using KorridorX.Dtos.Recipients;
using KorridorX.Dtos.Transfers;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
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

        var registerResponse = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequestDto(
                "Kay",
                "Tester",
                email,
                "ReleaseCandidate!123",
                "+12145550101",
                "US",
                UserType.Consumer));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.ReadApiResponseAsync<AuthResponseDto>();
        Assert.True(registered.Success);
        Assert.NotNull(registered.Data);
        client.UseBearerToken(registered.Data!.AccessToken);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

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
            "Release candidate end-to-end test");

        var transferResponse = await client.PostJsonAsync("/api/transfers", transferRequest);
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
