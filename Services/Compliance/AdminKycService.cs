using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public class AdminKycService : IAdminKycService
{
    private readonly AppDbContext _db;

    public AdminKycService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AdminKycApplicationListItemDto>> GetApplicationsAsync(
        KycStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.KycApplications
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.KycProfile.IsDeleted &&
                !x.KycProfile.CustomerProfile.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var paged = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminKycApplicationListItemDto(
                x.Id,
                x.KycProfile.CustomerProfileId,
                (x.KycProfile.CustomerProfile.FirstName + " " + x.KycProfile.CustomerProfile.LastName).Trim(),
                x.KycProfile.CustomerProfile.Email ?? x.KycProfile.CustomerProfile.User.Email,
                x.KycProfile.CustomerProfile.CountryCode,
                x.Status,
                x.ProviderApplicationId,
                x.SubmittedAt,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);

        return paged;
    }

    public async Task<AdminKycApplicationDetailsDto> GetApplicationAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var application = await _db.KycApplications
            .AsNoTracking()
            .Include(x => x.Documents)
            .Include(x => x.KycProfile)
            .ThenInclude(x => x.CustomerProfile)
            .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.Id == applicationId &&
                !x.IsDeleted &&
                !x.KycProfile.IsDeleted &&
                !x.KycProfile.CustomerProfile.IsDeleted,
                ct);

        if (application is null)
        {
            throw new InvalidOperationException("KYC application not found.");
        }

        var customer = application.KycProfile.CustomerProfile;

        return new AdminKycApplicationDetailsDto(
            customer.Id,
            $"{customer.FirstName} {customer.LastName}".Trim(),
            customer.Email ?? customer.User.Email,
            customer.CountryCode,
            customer.KycStatus,
            ToApplicationDto(application));
    }

    private static KycApplicationDto ToApplicationDto(KycApplication application)
    {
        var documents = application.Documents
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.DocumentType)
            .Select(x =>
            {
                if (!Enum.TryParse<KycDocumentType>(x.DocumentType, out var documentType))
                {
                    throw new InvalidOperationException(
                        $"Unsupported stored KYC document type '{x.DocumentType}'.");
                }

                return new KycDocumentDto(
                    x.Id,
                    documentType,
                    x.FileName,
                    x.MimeType,
                    x.IsUploaded,
                    x.UploadConfirmedAt,
                    x.IsAttachedToProvider,
                    x.AttachedToProviderAt,
                    x.RejectionReason,
                    x.CreatedAt);
            })
            .ToList();

        return new KycApplicationDto(
            application.Id,
            application.KycProfileId,
            application.Status,
            application.ProviderApplicationId,
            application.IdentityType,
            application.IdentityNumberLastFour,
            application.IdentityIssueDate,
            application.IdentityExpiryDate,
            application.SubmittedAt,
            application.ReviewedAt,
            application.ReviewNote,
            documents,
            application.CreatedAt,
            application.LastUpdatedAt);
    }
}
