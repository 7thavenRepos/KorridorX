using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance.Blaaiz;

namespace KorridorX.Services.BusinessBeneficiaries;

/// <summary>
/// CAD Interac is an email destination, not a bank account or mobile wallet.
/// Contact names must be explicitly supplied; no fabricated name fallback.
/// Format validation never proves that an email belongs to the intended payee.
/// </summary>
public static class BusinessBeneficiaryInteracPolicy
{
    public static void Validate(BusinessBeneficiary beneficiary)
    {
        if (beneficiary.IsDeleted || !beneficiary.IsActive ||
            !string.Equals(beneficiary.CountryCode, "CA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An active Canadian beneficiary is required for CAD Interac.");

        BlaaizCurrencyRequirements.Validate(
            "CAD", PaymentMethod.Interac, beneficiary.CountryCode,
            new Dictionary<string, string?>
            {
                ["email"] = beneficiary.Email,
                ["firstName"] = beneficiary.ContactFirstName,
                ["lastName"] = beneficiary.ContactLastName
            });
    }

    public static bool IsReady(BusinessBeneficiary beneficiary)
    {
        try { Validate(beneficiary); return true; }
        catch (InvalidOperationException) { return false; }
    }
}
