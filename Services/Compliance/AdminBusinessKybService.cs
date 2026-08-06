using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public class AdminBusinessKybService : IAdminBusinessKybService
{
    private readonly AppDbContext _db;

    public AdminBusinessKybService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AdminBusinessKybApplicationListItemDto>> GetApplicationsAsync(
        KybStatus? status,
        string? countryCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.BusinessKybApplications
            .AsNoTracking()
            .Where(x => !x.IsDeleted && !x.BusinessProfile.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var normalizedCountry = countryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.BusinessProfile.CountryCode == normalizedCountry);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x =>
                x.BusinessProfile.BusinessName.ToLower().Contains(normalizedSearch) ||
                (x.BusinessProfile.TradingName != null &&
                 x.BusinessProfile.TradingName.ToLower().Contains(normalizedSearch)) ||
                (x.BusinessProfile.RegistrationNumber != null &&
                 x.BusinessProfile.RegistrationNumber.ToLower().Contains(normalizedSearch)) ||
                (x.BusinessProfile.ContactEmail != null &&
                 x.BusinessProfile.ContactEmail.ToLower().Contains(normalizedSearch)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminBusinessKybApplicationListItemDto(
                x.Id,
                x.BusinessProfileId,
                x.BusinessProfile.BusinessName,
                x.BusinessProfile.RegistrationNumber,
                x.BusinessProfile.CountryCode,
                x.KybScope,
                x.Status,
                x.ProviderApplicationId,
                x.Owners.Count(o => !o.IsDeleted),
                x.Documents.Count(d => !d.IsDeleted),
                x.SubmittedAt,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<AdminBusinessKybApplicationDetailsDto> GetApplicationAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var application = await _db.BusinessKybApplications
            .AsNoTracking()
            .Include(x => x.BusinessProfile)
            .ThenInclude(x => x.OwnerUser)
            .Include(x => x.Owners)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x =>
                x.Id == applicationId &&
                !x.IsDeleted &&
                !x.BusinessProfile.IsDeleted,
                ct);

        if (application is null)
        {
            throw new InvalidOperationException("Business KYB application not found.");
        }

        return new AdminBusinessKybApplicationDetailsDto(
            ToApplicationDto(application),
            application.BusinessProfile.OwnerUser.Email ?? "");
    }

    private static BusinessKybApplicationDto ToApplicationDto(BusinessKybApplication application)
    {
        return new BusinessKybApplicationDto(
            application.Id,
            application.BusinessProfileId,
            application.Status,
            application.KybScope,
            application.ProviderApplicationId,
            application.SubmittedAt,
            application.ReviewedAt,
            application.ReviewNote,
            application.CreatedAt,
            ToProfileDto(application.BusinessProfile),
            application.Owners
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Select(ToOwnerDto)
                .ToList(),
            application.Documents
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DocumentType)
                .ThenBy(x => x.CreatedAt)
                .Select(ToDocumentDto)
                .ToList());
    }

    private static BusinessProfileKybDto ToProfileDto(BusinessProfile profile) => new(
        profile.Id,
        profile.BusinessName,
        profile.TradingName,
        profile.BusinessType,
        profile.RegistrationNumber,
        profile.TaxIdentificationNumber,
        profile.CountryCode,
        profile.IncorporationDate,
        profile.IndustryType,
        profile.BusinessDescription,
        profile.Website,
        profile.SourceOfFunds,
        profile.EstimatedAnnualRevenue,
        profile.ExpectedMonthlyPayments,
        profile.AccountPurpose,
        profile.KybScope,
        profile.StateOrProvince,
        profile.City,
        profile.AddressLine1,
        profile.AddressLine2,
        profile.PostalCode,
        profile.OperatingCountryCode,
        profile.OperatingStateOrProvince,
        profile.OperatingCity,
        profile.OperatingAddressLine1,
        profile.OperatingAddressLine2,
        profile.OperatingPostalCode,
        profile.ContactEmail,
        profile.ContactPhone,
        profile.KybStatus,
        profile.KybSubmittedAt,
        profile.KybApprovedAt,
        profile.KybRejectedAt,
        profile.KybRejectionReason,
        profile.BlaaizBusinessCustomerId);

    private static BusinessOwnerDto ToOwnerDto(BusinessBeneficialOwner owner) => new(
        owner.Id,
        owner.ProviderOwnerId,
        owner.FirstName,
        owner.LastName,
        owner.Email,
        owner.DateOfBirth,
        owner.Nationality,
        owner.CountryCode,
        owner.Title,
        owner.OwnershipPercentage,
        owner.HasControl,
        owner.IsSigner,
        owner.IsBeneficialOwner,
        owner.IdDocumentType,
        owner.IdentityNumberLastFour,
        owner.IdDocumentCountry,
        owner.IdExpiryDate,
        owner.IsPep,
        owner.IsIdentityFrontUploaded,
        owner.IsIdentityBackUploaded,
        owner.AreIdentityFilesAttachedToProvider,
        owner.ProviderStatus,
        owner.RejectionReason,
        owner.ProviderAdminCommentsJson);

    private static BusinessKybDocumentDto ToDocumentDto(BusinessKybDocument document) => new(
        document.Id,
        document.DocumentType,
        document.Name,
        document.Description,
        document.FileName,
        document.MimeType,
        document.ProviderFileId,
        document.ProviderDocumentId,
        document.IsUploaded,
        document.IsRegisteredWithProvider,
        document.ProviderStatus,
        document.RejectionReason,
        document.ProviderAdminCommentsJson);
}
