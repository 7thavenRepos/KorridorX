using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Providers;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Providers;

public class ProviderCustomerService : IProviderCustomerService
{
    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;

    public ProviderCustomerService(
        AppDbContext db,
        IRemittanceProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    public async Task<ProviderCustomerDto> SyncMyCustomerAsync(
        Guid userId,
        SyncProviderCustomerRequestDto request,
        CancellationToken ct = default)
    {
        var profile = await _db.CustomerProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                !x.IsDeleted,
                ct);

        if (profile is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
        }

        if (profile.CustomerType != CustomerType.Individual)
        {
            throw new InvalidOperationException(
                "This provider synchronization endpoint currently supports individual customers only.");
        }

        var mapping = await _db.ProviderCustomers
            .FirstOrDefaultAsync(x =>
                x.CustomerProfileId == profile.Id &&
                x.ProviderCode == _provider.ProviderCode &&
                !x.IsDeleted,
                ct);

        var email = profile.Email ?? profile.User.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Customer email is required before provider synchronization.");
        }

        var result = await _provider.SyncIndividualCustomerAsync(
            new RemittanceProviderCustomerRequest(
                profile.Id,
                mapping?.ProviderCustomerId ?? profile.BlaaizCustomerId,
                profile.FirstName,
                profile.LastName,
                email,
                profile.CountryCode,
                profile.PhoneNumber,
                profile.DateOfBirth,
                profile.AddressLine1,
                profile.City,
                profile.StateOrProvince,
                profile.PostalCode,
                request.IdType,
                request.IdNumber,
                request.IdIssueDate,
                request.IdExpiryDate),
            ct);

        var now = DateTime.UtcNow;

        if (mapping is null)
        {
            mapping = new ProviderCustomer
            {
                CustomerProfileId = profile.Id,
                ProviderCode = _provider.ProviderCode,
                ProviderCustomerId = result.ProviderCustomerId,
                ProviderStatus = result.ProviderStatus,
                MetadataJson = result.RawResponseJson,
                LastSyncedAt = now,
                CreatedByUserId = userId
            };

            _db.ProviderCustomers.Add(mapping);
        }
        else
        {
            mapping.ProviderCustomerId = result.ProviderCustomerId;
            mapping.ProviderStatus = result.ProviderStatus;
            mapping.MetadataJson = result.RawResponseJson;
            mapping.LastSyncedAt = now;
            mapping.LastUpdatedAt = now;
            mapping.LastUpdatedByUserId = userId;
        }

        profile.BlaaizCustomerId = result.ProviderCustomerId;
        profile.LastUpdatedAt = now;
        profile.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return ToDto(mapping);
    }

    public async Task<ProviderCustomerDto> GetMyProviderCustomerAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var mapping = await _db.ProviderCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.CustomerProfile != null &&
                x.CustomerProfile.UserId == userId &&
                x.ProviderCode == _provider.ProviderCode &&
                !x.IsDeleted &&
                !x.CustomerProfile.IsDeleted,
                ct);

        if (mapping is null || mapping.CustomerProfileId is null)
        {
            throw new InvalidOperationException("Provider customer has not been created yet.");
        }

        return ToDto(mapping);
    }

    private static ProviderCustomerDto ToDto(ProviderCustomer mapping)
    {
        if (mapping.CustomerProfileId is null)
        {
            throw new InvalidOperationException("Provider customer is not linked to a customer profile.");
        }

        return new ProviderCustomerDto(
            mapping.Id,
            mapping.CustomerProfileId.Value,
            mapping.ProviderCode.ToString(),
            mapping.ProviderCustomerId,
            mapping.ProviderStatus,
            mapping.LastSyncedAt,
            mapping.CreatedAt);
    }
}
