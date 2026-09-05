using KorridorX.Data;
using KorridorX.Dtos.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Customers;

public class CustomerProfileService : ICustomerProfileService
{
    private readonly AppDbContext _db;
    private readonly IComplianceScreeningService _screeningService;

    public CustomerProfileService(
        AppDbContext db,
        IComplianceScreeningService screeningService)
    {
        _db = db;
        _screeningService = screeningService;
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
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        if (profile is null)
            throw new InvalidOperationException("Customer profile not found.");

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var countryCode = request.CountryCode.Trim().ToUpperInvariant();
        var dateOfBirth = request.DateOfBirth.HasValue
            ? DateTime.SpecifyKind(request.DateOfBirth.Value.Date, DateTimeKind.Utc)
            : (DateTime?)null;

        if (firstName.Length == 0)
            throw new InvalidOperationException("First name is required.");
        if (lastName.Length == 0)
            throw new InvalidOperationException("Last name is required.");
        if (dateOfBirth > DateTime.UtcNow.Date)
            throw new InvalidOperationException("Date of birth cannot be in the future.");

        var isSupportedSendCountry = await _db.Countries
            .AsNoTracking()
            .AnyAsync(
                x => x.Code == countryCode &&
                     x.IsSupported &&
                     x.IsSendCountry,
                ct);

        if (!isSupportedSendCountry)
            throw new InvalidOperationException(
                $"Country '{countryCode}' is not available for Consumer accounts.");

        profile.FirstName = firstName;
        profile.LastName = lastName;
        profile.MiddleName = Clean(request.MiddleName);
        profile.DateOfBirth = dateOfBirth;
        profile.PhoneNumber = Clean(request.PhoneNumber);
        profile.CountryCode = countryCode;
        profile.StateOrProvince = Clean(request.StateOrProvince);
        profile.City = Clean(request.City);
        profile.AddressLine1 = Clean(request.AddressLine1);
        profile.AddressLine2 = Clean(request.AddressLine2);
        profile.PostalCode = Clean(request.PostalCode);
        profile.LastUpdatedAt = DateTime.UtcNow;

        profile.User.FirstName = firstName;
        profile.User.LastName = lastName;
        profile.User.PhoneNumber = profile.PhoneNumber;
        profile.User.CountryCode = countryCode;
        profile.User.LastUpdatedAt = profile.LastUpdatedAt;

        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenCustomerAsync(
            profile.Id,
            ScreeningReason.ProfileChanged,
            userId,
            null,
            ct);
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

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
