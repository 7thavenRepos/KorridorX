using KorridorX.Providers.Remittance.Blaaiz;

namespace KorridorX.Services.Recipients;

public static class RecipientDeliveryPolicy
{
    public const string EmailTransfer = "EmailTransfer";
    public const string BankAccount = "BankAccount";
    public static string? Resolve(string? destinationCountry, string? destinationAsset)
    {
        if (!BlaaizCurrencyRequirements.SupportsCountry(destinationAsset, destinationCountry)) return null;
        var requirement = BlaaizCurrencyRequirements.Find(destinationAsset)!;
        return requirement.PayoutMethods.Any(x => x.PaymentMethod == KorridorX.Models.Enums.PaymentMethod.Interac)
            ? EmailTransfer : BankAccount;
    }
    public static bool IsEmailDelivery(string? country, string? asset) => Resolve(country, asset) == EmailTransfer;
    public static IReadOnlyList<RecipientDeliveryOption> Available { get; } =
        BlaaizCurrencyRequirements.All.Select(x => new RecipientDeliveryOption(x.CountryCode, x.CurrencyCode,
            x.PayoutMethods.Any(m => m.PaymentMethod == KorridorX.Models.Enums.PaymentMethod.Interac)
                ? EmailTransfer : BankAccount, x.PayoutMethods, x.BankCountryCodes)).ToArray();
}
public sealed record RecipientDeliveryOption(string CountryCode, string AssetCode, string Mode,
    IReadOnlyList<BlaaizPayoutMethodRequirement>? PayoutMethods = null,
    IReadOnlyList<string>? BankCountryCodes = null);
