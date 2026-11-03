using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessBeneficiaries;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Payments;
using Xunit;

namespace KorridorX.Tests;

public sealed class BusinessBeneficiaryInteracPolicyTests
{
    [Fact]
    public void Cad_interac_uses_explicit_contact_names_and_email_not_bank_verification()
    {
        var beneficiary = NewRecipient();
        Assert.Empty(beneficiary.BankAccounts);
        Assert.True(BusinessBeneficiaryInteracPolicy.IsReady(beneficiary));
        BusinessBeneficiaryInteracPolicy.Validate(beneficiary);

        beneficiary.Email = "not an email";
        Assert.False(BusinessBeneficiaryInteracPolicy.IsReady(beneficiary));
        beneficiary.Email = "ada@example.ca";
        beneficiary.ContactLastName = "";
        Assert.False(BusinessBeneficiaryInteracPolicy.IsReady(beneficiary));
        beneficiary.ContactLastName = "Example";
        beneficiary.CountryCode = "GB";
        Assert.False(BusinessBeneficiaryInteracPolicy.IsReady(beneficiary));
        beneficiary.CountryCode = "CA";
        beneficiary.IsActive = false;
        Assert.False(BusinessBeneficiaryInteracPolicy.IsReady(beneficiary));
    }

    [Fact]
    public void Cad_quote_recipient_filter_is_tenant_scoped_and_does_not_require_a_bank()
    {
        var businessId = Guid.NewGuid();
        var valid = NewRecipient();
        valid.BusinessProfileId = businessId;
        var otherBusiness = NewRecipient();
        otherBusiness.BusinessProfileId = Guid.NewGuid();
        var incomplete = NewRecipient();
        incomplete.BusinessProfileId = businessId;
        incomplete.ContactFirstName = null;
        var criteria = BusinessTransferRecipientEligibilityPolicy.ForQuote(
            businessId, BusinessBeneficiaryType.Individual, "CAD", ["CA"]);

        var options = new[] { valid, otherBusiness, incomplete }.AsQueryable()
            .Where(criteria)
            .Where(x => BusinessTransferRecipientEligibilityPolicy.HasReadyDestination(x, "CAD"))
            .ToArray();
        Assert.Single(options);
        Assert.Same(valid, options[0]);
    }

    [Fact]
    public void Cad_interac_is_explicitly_allowed_only_for_blaaiz_provider()
    {
        PayoutDestinationCapabilityPolicy.EnsureBusinessInteracSupported("Blaaiz", "CAD");
        Assert.Throws<InvalidOperationException>(() =>
            PayoutDestinationCapabilityPolicy.EnsureBusinessInteracSupported("Blaaiz", "GBP"));
        Assert.Throws<InvalidOperationException>(() =>
            PayoutDestinationCapabilityPolicy.EnsureBusinessInteracSupported("NotAProvider", "CAD"));
    }

    private static BusinessBeneficiary NewRecipient() => new()
    {
        CountryCode = "CA",
        BeneficiaryType = BusinessBeneficiaryType.Individual,
        IsActive = true,
        Name = "Ada Example",
        ContactFirstName = "Ada",
        ContactLastName = "Example",
        Email = "ada@example.ca"
    };
}
