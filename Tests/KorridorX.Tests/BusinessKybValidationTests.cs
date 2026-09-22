using System.Net;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Middleware;
using KorridorX.Models.Customers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace KorridorX.Tests;

public sealed class BusinessKybValidationTests
{
    private const string ProviderBody = """
        {"message":"The selected business type is invalid. (and 4 more errors)",
         "errors":{"business_type":["Select a supported business type."],"phone":["Enter a valid phone number."],
           "source_of_funds":["Select a source of funds."],"estimated_annual_revenue":["Select a revenue band."],
           "owners.0.id_document_number":["Document number is required.","Document number format is invalid."]},
         "data":{"private_document":"DO_NOT_RETURN"},"access_token":"DO_NOT_RETURN"}
        """;

    [Theory]
    [InlineData(400)]
    [InlineData(422)]
    public async Task Customer_validation_preserves_every_field_and_message_without_raw_provider_payload(int status)
    {
        var errors = BlaaizValidationErrors.Read("/api/external/customer", status, ProviderBody);
        Assert.NotNull(errors); Assert.Equal(5, errors.Count);
        var ex = new ProviderIntegrationException("aggregate", status, ProviderBody, Guid.NewGuid(), validationErrors: errors);
        var (code, body) = await Response(ex);
        Assert.Equal(422, code);
        Assert.Equal("VALIDATION_ERROR", body.RootElement.GetProperty("error").GetProperty("code").GetString());
        var context = body.RootElement.GetProperty("error").GetProperty("details").GetProperty("context");
        Assert.Equal(2, context.GetProperty("validationErrors").GetProperty("owners.0.id_document_number").GetArrayLength());
        Assert.DoesNotContain("DO_NOT_RETURN", body.RootElement.GetRawText());
        Assert.DoesNotContain("and 4 more", body.RootElement.GetRawText());
        Assert.Equal(status, context.GetProperty("providerStatusCode").GetInt32());
    }

    [Theory]
    [InlineData("/oauth/token", 422, "{}")]
    [InlineData("/api/external/customer", 403, "{}")]
    [InlineData("/api/external/customer", 500, "{}")]
    [InlineData("/api/external/customer", 422, "<html>Forbidden</html>")]
    [InlineData("/api/external/customer", 400, "{\"error\":\"bad request\"}")]
    [InlineData("/api/external/wallet", 422, "{}")]
    public async Task Authentication_outages_and_non_validation_failures_keep_the_provider_error_contract(string endpoint, int status, string payload)
    {
        var errors = BlaaizValidationErrors.Read(endpoint, status, payload);
        Assert.Null(errors);
        var (code, _) = await Response(new ProviderIntegrationException("provider failure", status, payload, validationErrors: errors));
        Assert.Equal(502, code);
    }

    [Fact]
    public void Single_strings_and_unknown_field_names_are_retained_but_non_message_objects_are_not_forwarded()
    {
        var errors = BlaaizValidationErrors.Read("/api/external/customer/id/submit", 422,
            """{"errors":{"future_field":"Please correct this field.","name":[null,7,"Required","Required"],"payload":{"secret":"do-not-forward"}}}""");
        Assert.Equal("Please correct this field.", Assert.Single(errors!["future_field"]));
        Assert.Equal("Required", Assert.Single(errors["name"]));
        Assert.False(errors.ContainsKey("payload"));
    }

    [Fact]
    public async Task Local_profile_validation_collects_missing_fields_and_marks_business_type()
    {
        var ex = Assert.Throws<RequestValidationException>(() => BusinessKybProfileValidation.Validate(new BusinessProfile(), false));
        Assert.Equal(7, ex.ValidationErrors.Count);
        foreach (var field in new[] { "businessName", "registrationNumber", "countryCode", "contactEmail", "addressLine1", "city", "businessType" })
            Assert.True(ex.ValidationErrors.ContainsKey(field));
        var (code, body) = await Response(ex);
        Assert.Equal(422, code);
        Assert.Contains("businessType", body.RootElement.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Only_an_active_master_data_business_type_can_be_synchronized(bool active)
    {
        var profile = ValidProfile();
        if (active) BusinessKybProfileValidation.Validate(profile, true);
        else Assert.Contains("businessType", Assert.Throws<RequestValidationException>(() => BusinessKybProfileValidation.Validate(profile, false)).ValidationErrors.Keys);
    }

    [Fact]
    public void Invalid_provider_choices_and_profile_values_are_reported_together()
    {
        var profile = ValidProfile();
        profile.SourceOfFunds = "some income"; profile.EstimatedAnnualRevenue = "10000";
        profile.AccountPurpose = "business"; profile.Website = "example.com";
        profile.ExpectedMonthlyPayments = -1; profile.ContactEmail = "not-an-email";
        profile.IncorporationDate = DateTime.UtcNow.AddDays(1);
        var errors = Assert.Throws<RequestValidationException>(() => BusinessKybProfileValidation.Validate(profile, true)).ValidationErrors;
        Assert.Equal(7, errors.Count);
    }

    private static BusinessProfile ValidProfile() => new()
    {
        BusinessName = "Example Ltd", BusinessType = "corporation", RegistrationNumber = "RC-TEST",
        CountryCode = "NG", ContactEmail = "team@example.test", AddressLine1 = "100 Example Street", City = "Lagos",
        SourceOfFunds = "business_revenue", EstimatedAnnualRevenue = "0_99999",
        AccountPurpose = "receive_payments_for_goods_and_services"
    };

    private static async Task<(int Status, JsonDocument Body)> Response(Exception exception)
    {
        var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream(); context.TraceIdentifier = "test-correlation";
        await new GlobalExceptionMiddleware(_ => throw exception, NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(context);
        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await JsonDocument.ParseAsync(context.Response.Body));
    }
}
