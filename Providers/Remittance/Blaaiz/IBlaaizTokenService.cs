namespace KorridorX.Providers.Remittance.Blaaiz;

public interface IBlaaizTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);

    // A custom implementation must opt in to the scoped read path; never silently use a broader token.
    Task<string> GetInteracAutoDepositReadAccessTokenAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("This Blaaiz token service does not support Interac registration reads.");

    // Fee quotation needs fees:read, isolated from the production payout token.
    Task<string> GetFeeReadAccessTokenAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("This Blaaiz token service does not support fee reads.");
}
