namespace KorridorX.Services.Recipients;

/// <summary>
/// Consumer payout instructions for provider rails implemented in KorridorX.
/// Showing an asset in the currency picker does not make its payout rail available.
/// Only these currently implemented country/currency pairs are presented as
/// consumer recipient options. Other assets may still be exchanged into own wallets.
/// </summary>
public static class RecipientDeliveryPolicy
{
    public const string EmailTransfer = "EmailTransfer";
    public const string BankAccount = "BankAccount";

    public static string? Resolve(string? destinationCountry, string? destinationAsset)
    {
        var country = destinationCountry?.Trim().ToUpperInvariant();
        var asset = destinationAsset?.Trim().ToUpperInvariant();
        return (country, asset) switch
        {
            ("CA", "CAD") => EmailTransfer,
            ("NG", "NGN") => BankAccount,
            _ => null
        };
    }

    public static bool IsEmailDelivery(string? country, string? asset) =>
        Resolve(country, asset) == EmailTransfer;

    public static IReadOnlyList<RecipientDeliveryOption> Available { get; } =
        new[]
        {
            new RecipientDeliveryOption("CA", "CAD", EmailTransfer),
            new RecipientDeliveryOption("NG", "NGN", BankAccount)
        };
}

public sealed record RecipientDeliveryOption(
    string CountryCode,
    string AssetCode,
    string Mode);
