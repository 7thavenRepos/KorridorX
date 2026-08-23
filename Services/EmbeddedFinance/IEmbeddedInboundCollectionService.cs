namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedInboundCollectionService
{
    Task<bool> TryProcessBlaaizDepositAsync(
        EmbeddedInboundCollectionRequest request,
        CancellationToken ct = default);
}

public sealed record EmbeddedInboundCollectionRequest(
    string ProviderTransactionId,
    string? ProviderReference,
    string ProviderStatus,
    string CurrencyCode,
    decimal Amount,
    decimal? AmountWithoutFee,
    decimal? ProviderFeeAmount,
    string? ProviderFeeCurrencyCode,
    string? ProviderAccountId,
    string? ProviderCustomerId,
    string? ProviderAccountReference,
    string? AccountNumber,
    string RawPayloadJson,
    DateTime OccurredAt);
