namespace KorridorX.Services.EmbeddedFinance;

public interface ICollectionAccountProvisioner
{
    string ProviderCode { get; }
    bool Supports(string countryCode, string assetCode);
    Task<CollectionAccountProvisioningResult> ProvisionAsync(
        CollectionAccountProvisioningRequest request,
        CancellationToken ct = default);
}

public sealed record CollectionAccountProvisioningRequest(
    Guid BusinessProfileId,
    Guid BusinessCustomerId,
    Guid CollectionAccountId,
    string ExternalReference,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    string CountryCode,
    string AssetCode);

public sealed record CollectionAccountProvisioningResult(
    string? ProviderCustomerId,
    string ProviderAccountId,
    string? ProviderReference,
    string? AccountNumber,
    string? AccountName,
    string? BankName,
    string? MetadataJson);
