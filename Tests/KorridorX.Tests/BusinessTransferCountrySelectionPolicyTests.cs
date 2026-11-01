using KorridorX.Services.BusinessTransfers;

namespace KorridorX.Tests;

public sealed class BusinessTransferCountrySelectionPolicyTests
{
    [Fact]
    public void Prefers_eligible_business_operating_country_over_registered_country()
    {
        Assert.Equal("GB", BusinessTransferCountrySelectionPolicy.Source("NG", "GB", "GBP", ["NG", "GB"]));
    }

    [Fact]
    public void Uses_registered_country_if_operating_country_is_not_eligible()
    {
        Assert.Equal("NG", BusinessTransferCountrySelectionPolicy.Source("NG", "GB", "NGN", ["NG"]));
    }

    [Fact]
    public void Rejects_a_source_currency_without_an_eligible_business_country()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BusinessTransferCountrySelectionPolicy.Source("NG", "GB", "CAD", ["CA"]));
    }

    [Fact]
    public void Automatically_resolves_a_unique_receiving_country()
    {
        Assert.Equal("GB", BusinessTransferCountrySelectionPolicy.Destination("GBP", ["GB"], null));
    }

    [Fact]
    public void Uses_a_verified_beneficiary_country_when_multiple_routes_exist()
    {
        Assert.Equal("US", BusinessTransferCountrySelectionPolicy.Destination("USD", ["US", "NG"], "US"));
    }

    [Fact]
    public void Does_not_guess_a_multi_country_destination()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            BusinessTransferCountrySelectionPolicy.Destination("USD", ["US", "NG"], null));
        Assert.Contains("Select a beneficiary", error.Message);
    }

    [Fact]
    public void Rejects_a_beneficiary_in_an_ineligible_country()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BusinessTransferCountrySelectionPolicy.Destination("USD", ["US", "CA"], "NG"));
    }

    [Fact]
    public void Rejects_no_enabled_destination_country()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BusinessTransferCountrySelectionPolicy.Destination("GBP", [], null));
    }
}
