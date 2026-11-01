namespace KorridorX.Services.BusinessTransfers;

/// <summary>
/// A quote's country metadata is derived from authorized business and master-data
/// settings. Never choose a country arbitrarily for a multi-country payout currency.
/// </summary>
public static class BusinessTransferCountrySelectionPolicy
{
    public static string Source(string registered, string? operating, string currency,
        IReadOnlyCollection<string> eligibleCountries)
    {
        if (!string.IsNullOrWhiteSpace(operating) && eligibleCountries.Contains(operating)) return operating;
        if (eligibleCountries.Contains(registered)) return registered;
        throw new InvalidOperationException(
            $"The selected business has no eligible sending country for {currency}. Review its registered or operating country and corridor configuration.");
    }

    public static string Destination(string currency, IReadOnlyCollection<string> eligibleCountries,
        string? verifiedBeneficiaryCountry)
    {
        if (!string.IsNullOrWhiteSpace(verifiedBeneficiaryCountry))
        {
            if (eligibleCountries.Contains(verifiedBeneficiaryCountry)) return verifiedBeneficiaryCountry;
            throw new InvalidOperationException(
                $"The selected beneficiary country is not an enabled payout country for {currency}.");
        }
        if (eligibleCountries.Count == 1) return eligibleCountries.Single();
        if (eligibleCountries.Count == 0)
            throw new InvalidOperationException($"No receiving country is enabled for {currency} business transfers.");
        throw new InvalidOperationException(
            $"Multiple receiving countries are configured for {currency}. Select a beneficiary to determine the destination.");
    }
}
