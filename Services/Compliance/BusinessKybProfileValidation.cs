using System.ComponentModel.DataAnnotations;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;

namespace KorridorX.Services.Compliance;

public static class BusinessKybProfileValidation
{
    public static void Validate(BusinessProfile profile, bool businessTypeActive)
    {
        var errors = new Dictionary<string, string[]>();
        void Required(string key, string? value, string label)
        { if (string.IsNullOrWhiteSpace(value)) errors[key] = [$"{label} is required."]; }
        void Choice(string key, string? value, params string[] allowed)
        { if (value is not null && !allowed.Contains(value)) errors[key] = ["Select one of the available options."]; }
        Required("businessName", profile.BusinessName, "Legal business name");
        Required("registrationNumber", profile.RegistrationNumber, "Registration number");
        Required("countryCode", profile.CountryCode, "Country of registration");
        Required("contactEmail", profile.ContactEmail, "Contact email");
        Required("addressLine1", profile.AddressLine1, "Registered address");
        Required("city", profile.City, "City");
        if (!businessTypeActive) errors["businessType"] = ["Select an active business type from the list."];
        if (!string.IsNullOrWhiteSpace(profile.ContactEmail) && !new EmailAddressAttribute().IsValid(profile.ContactEmail))
            errors["contactEmail"] = ["Enter a valid email address."];
        if (!string.IsNullOrWhiteSpace(profile.CountryCode) && profile.CountryCode.Length != 2)
            errors["countryCode"] = ["Select a country from the list."];
        if (profile.IncorporationDate?.Date >= DateTime.UtcNow.Date)
            errors["incorporationDate"] = ["Incorporation date must be in the past."];
        if (profile.Website is not null && (!Uri.TryCreate(profile.Website, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")))
            errors["website"] = ["Enter a complete website address starting with https:// or http://."];
        if (profile.BusinessDescription?.Length > 2000) errors["businessDescription"] = ["Use at most 2,000 characters."];
        if (profile.ExpectedMonthlyPayments is < 0 or > 1000000000) errors["expectedMonthlyPayments"] = ["Enter a whole number from 0 to 1,000,000,000."];
        Choice("sourceOfFunds", profile.SourceOfFunds, "business_revenue", "business_loans", "investment_income", "third_party_funds", "other");
        Choice("estimatedAnnualRevenue", profile.EstimatedAnnualRevenue, "0_99999", "100000_499999", "500000_999999", "1000000_4999999", "5000000_24999999", "25000000_99999999", "100000000_249999999", "250000000_plus");
        Choice("accountPurpose", profile.AccountPurpose, "receive_payments_for_goods_and_services", "send_payments_for_goods_and_services", "send_receive_funds_related_parties", "other");
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
}
