namespace KorridorX.Services.PaymentChannels;

/// <summary>
/// Foreign business receiving accounts must be issued to the business's
/// verified primary owner as an individual. This never reclassifies a Blaaiz
/// business customer as an individual, nor changes who owns the wallet.
/// </summary>
public static class BusinessPrincipalReceivingAccountPolicy
{
    public static bool IsForeignVirtualAccountCurrency(string assetCode) =>
        assetCode is "USD" or "GBP" or "EUR";

    public static void RequireCorrectRoute(bool business, string assetCode, string capabilityCode)
    {
        var principalRoute = capabilityCode == BlaaizChannelCapabilities.BusinessPrincipalVirtualAccount;
        if (business && IsForeignVirtualAccountCurrency(assetCode) && !principalRoute)
            throw new InvalidOperationException("Foreign business bank deposits require the verified-primary-holder channel. A business-customer virtual account must not be used.");
        if (principalRoute && (!business || !IsForeignVirtualAccountCurrency(assetCode)))
            throw new InvalidOperationException("The primary-holder deposit channel is restricted to USD, GBP and EUR business wallets.");
    }

    public static void RequireOwnerConsent(Guid actorUserId, Guid ownerUserId, bool acknowledged)
    {
        if (actorUserId == Guid.Empty || ownerUserId == Guid.Empty || actorUserId != ownerUserId)
            throw new UnauthorizedAccessException("Only the business's registered primary account holder may authorize an account in their personal name.");
        if (!acknowledged)
            throw new InvalidOperationException("The primary account holder must acknowledge the personal-account ownership and business-wallet allocation before requesting bank details.");
    }

    /// <summary>Validate the production provider's current identity, not only a locally cached KYC state.</summary>
    public static void RequireVerifiedProviderCustomer(string expectedId, string? actualId,
        string? actualType, string? status, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(expectedId) ||
            !string.Equals(expectedId, actualId, StringComparison.Ordinal) ||
            !string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(status, "VERIFIED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Blaaiz has not confirmed a VERIFIED {expectedType} customer matching the saved provider identity. Account creation was stopped.");
    }
}
