using KorridorX.Data;
using KorridorX.Dtos.Customers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Customers;

public class CustomerProfileService : ICustomerProfileService
{
    private readonly AppDbContext _db;

    public CustomerProfileService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerProfileDto> GetMyProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        if (profile is null)
            throw new InvalidOperationException("Customer profile not found.");

        return Map(profile);
    }

    public async Task<CustomerProfileDto> UpdateMyProfileAsync(
        Guid userId,
        UpdateCustomerProfileRequestDto request,
        CancellationToken ct = default)
    {
        var profile = await _db.CustomerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        if (profile is null)
            throw new InvalidOperationException("Customer profile not found.");

        profile.FirstName = request.FirstName.Trim();
        profile.LastName = request.LastName.Trim();
        profile.MiddleName = request.MiddleName;
        profile.DateOfBirth = request.DateOfBirth;
        profile.PhoneNumber = request.PhoneNumber;
        profile.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        profile.StateOrProvince = request.StateOrProvince;
        profile.City = request.City;
        profile.AddressLine1 = request.AddressLine1;
        profile.AddressLine2 = request.AddressLine2;
        profile.PostalCode = request.PostalCode;
        profile.LastUpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Map(profile);
    }

    private static CustomerProfileDto Map(Models.Customers.CustomerProfile profile)
    {
        return new CustomerProfileDto(
            profile.Id,
            profile.UserId,
            profile.CustomerType,
            profile.FirstName,
            profile.LastName,
            profile.MiddleName,
            profile.DateOfBirth,
            profile.PhoneNumber,
            profile.Email,
            profile.CountryCode,
            profile.StateOrProvince,
            profile.City,
            profile.AddressLine1,
            profile.AddressLine2,
            profile.PostalCode,
            profile.KycStatus,
            profile.CreatedAt
        );
    }
}