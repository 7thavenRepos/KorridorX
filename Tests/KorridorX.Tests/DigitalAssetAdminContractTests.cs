using System.ComponentModel.DataAnnotations;
using KorridorX.Dtos.DigitalAssets;

namespace KorridorX.Tests;

public sealed class DigitalAssetAdminContractTests
{
    [Fact]
    public void Provider_configuration_change_requires_reason()
    {
        var request = new UpsertDigitalAssetProviderConfigurationRequestDto
        {
            ProviderCode = "BLAAIZ",
            DisplayName = "Blaaiz",
            Reason = ""
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void Provider_operator_action_requires_reason()
    {
        var request = new DigitalAssetProviderAdminActionRequestDto
        {
            Reason = ""
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void Provider_operator_action_accepts_reason()
    {
        var request = new DigitalAssetProviderAdminActionRequestDto
        {
            Reason = "Manual provider health validation after an incident."
        };

        Assert.True(IsValid(request));
    }

    private static bool IsValid(object value)
    {
        var context = new ValidationContext(value);
        var results = new List<ValidationResult>();

        return Validator.TryValidateObject(
            value,
            context,
            results,
            validateAllProperties: true);
    }
}