using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Dtos.Providers;
using KorridorX.Models.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public class KycService : IKycService
{
    private static readonly HashSet<string> AllowedIdentityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "drivers_license",
        "passport",
        "resident_permit"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private readonly AppDbContext _db;
    private readonly IProviderCustomerService _providerCustomerService;
    private readonly IRemittanceProvider _provider;

    public KycService(
        AppDbContext db,
        IProviderCustomerService providerCustomerService,
        IRemittanceProvider provider)
    {
        _db = db;
        _providerCustomerService = providerCustomerService;
        _provider = provider;
    }

    public async Task<KycApplicationDto> StartMyApplicationAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var profile = await _db.CustomerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        if (profile is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
        }

        if (profile.CustomerType != CustomerType.Individual)
        {
            throw new InvalidOperationException("This KYC flow currently supports individual customers only.");
        }

        var kycProfile = await _db.KycProfiles
            .Include(x => x.Applications)
            .ThenInclude(x => x.Documents)
            .FirstOrDefaultAsync(x => x.CustomerProfileId == profile.Id && !x.IsDeleted, ct);

        if (kycProfile is null)
        {
            kycProfile = new KycProfile
            {
                CustomerProfileId = profile.Id,
                Status = KycStatus.Pending,
                ProviderCode = _provider.ProviderName,
                CreatedByUserId = userId
            };

            _db.KycProfiles.Add(kycProfile);
        }

        var current = kycProfile.Applications
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (current is null || current.Status is KycStatus.Approved or KycStatus.Expired)
        {
            if (current?.Status == KycStatus.Approved)
            {
                return ToApplicationDto(current);
            }

            current = new KycApplication
            {
                KycProfile = kycProfile,
                KycProfileId = kycProfile.Id,
                Status = KycStatus.Pending,
                CreatedByUserId = userId
            };

            kycProfile.Applications.Add(current);
        }

        if (profile.KycStatus == KycStatus.NotStarted)
        {
            profile.KycStatus = KycStatus.Pending;
            profile.LastUpdatedAt = DateTime.UtcNow;
            profile.LastUpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);
        return ToApplicationDto(current);
    }

    public async Task<KycStatusDto> GetMyStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var kycProfile = await _db.KycProfiles
            .AsNoTracking()
            .Include(x => x.Applications)
            .ThenInclude(x => x.Documents)
            .FirstOrDefaultAsync(x =>
                x.CustomerProfile.UserId == userId &&
                !x.IsDeleted &&
                !x.CustomerProfile.IsDeleted,
                ct);

        if (kycProfile is null)
        {
            throw new InvalidOperationException("KYC has not been started.");
        }

        var current = kycProfile.Applications
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        return new KycStatusDto(
            kycProfile.Id,
            kycProfile.Status,
            kycProfile.ProviderCode,
            kycProfile.ProviderKycId,
            kycProfile.SubmittedAt,
            kycProfile.ApprovedAt,
            kycProfile.RejectedAt,
            kycProfile.RejectionReason,
            current is null ? null : ToApplicationDto(current));
    }

    public async Task<KycApplicationDto> SaveIdentityAsync(
        Guid userId,
        Guid applicationId,
        SaveKycIdentityRequestDto request,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        var idType = request.IdType.Trim().ToLowerInvariant();
        if (!AllowedIdentityTypes.Contains(idType))
        {
            throw new InvalidOperationException(
                "Identification type must be drivers_license, passport, or resident_permit.");
        }

        var idNumber = request.IdNumber.Trim();
        if (idNumber.Length < 4)
        {
            throw new InvalidOperationException("Identification number must contain at least four characters.");
        }

        if (request.IdExpiryDate.HasValue && request.IdExpiryDate.Value.Date <= DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException("Identification document must not be expired.");
        }

        if (!string.IsNullOrWhiteSpace(application.ProviderApplicationId) &&
            (!string.Equals(application.IdentityType, idType, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(application.IdentityNumberLastFour, idNumber[^4..], StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Identity details cannot be replaced after the provider customer has been created. Use the provider correction workflow.");
        }

        var customer = application.KycProfile.CustomerProfile;
        ValidateCustomerIdentityPrerequisites(customer);

        var providerCustomer = await _providerCustomerService.SyncMyCustomerAsync(
            userId,
            new SyncProviderCustomerRequestDto(
                idType,
                idNumber,
                request.IdIssueDate,
                request.IdExpiryDate),
            ct);

        var now = DateTime.UtcNow;
        application.ProviderApplicationId = providerCustomer.ProviderCustomerId;
        application.IdentityType = idType;
        application.IdentityNumberLastFour = idNumber[^4..];
        application.IdentityIssueDate = request.IdIssueDate;
        application.IdentityExpiryDate = request.IdExpiryDate;
        application.Status = MapProviderStatus(providerCustomer.ProviderStatus, KycStatus.Pending);
        application.SubmittedPayloadJson = JsonSerializer.Serialize(new
        {
            idType,
            idNumber = $"***{idNumber[^4..]}",
            request.IdIssueDate,
            request.IdExpiryDate
        });
        application.ReviewNote = null;
        application.LastUpdatedAt = now;
        application.LastUpdatedByUserId = userId;

        application.KycProfile.ProviderKycId = providerCustomer.ProviderCustomerId;
        application.KycProfile.ProviderCode = providerCustomer.ProviderCode;
        application.KycProfile.Status = application.Status;
        application.KycProfile.RejectionReason = null;
        application.KycProfile.LastUpdatedAt = now;
        application.KycProfile.LastUpdatedByUserId = userId;

        customer.BlaaizCustomerId = providerCustomer.ProviderCustomerId;
        customer.KycStatus = application.Status;
        customer.LastUpdatedAt = now;
        customer.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return ToApplicationDto(application);
    }

    public async Task<KycDocumentUploadUrlDto> CreateDocumentUploadUrlAsync(
        Guid userId,
        Guid applicationId,
        CreateKycDocumentUploadUrlRequestDto request,
        CancellationToken ct = default)
    {
        // Do not keep the tracked KYC aggregate alive across the external provider
        // call. Provider token/audit work can save through the same scoped DbContext,
        // and the request can spend meaningful time outside our process.
        var snapshot = await GetOwnedApplicationAsync(userId, applicationId, false, ct);
        EnsureEditable(snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.ProviderApplicationId))
        {
            throw new InvalidOperationException("Save identity information before uploading KYC documents.");
        }

        if (!AllowedMimeTypes.Contains(request.MimeType.Trim()))
        {
            throw new InvalidOperationException("KYC documents must be PDF, JPEG, or PNG files.");
        }

        var expectedProviderApplicationId = snapshot.ProviderApplicationId;
        var result = await _provider.RequestIndividualKycUploadUrlAsync(
            new RemittanceKycUploadUrlRequest(
                snapshot.KycProfile.CustomerProfileId,
                expectedProviderApplicationId,
                request.DocumentType),
            ct);

        return await PersistDocumentUploadRequestAsync(
            userId,
            applicationId,
            expectedProviderApplicationId,
            request,
            result,
            ct);
    }

    private async Task<KycDocumentUploadUrlDto> PersistDocumentUploadRequestAsync(
        Guid userId,
        Guid applicationId,
        string expectedProviderApplicationId,
        CreateKycDocumentUploadUrlRequestDto request,
        RemittanceKycUploadUrlResult result,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            // The provider has already created the signed upload request. Retry only
            // local persistence so a transient EF conflict never creates a second
            // provider-side upload artifact.
            _db.ChangeTracker.Clear();

            var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
            EnsureEditable(application);

            if (!string.Equals(
                    application.ProviderApplicationId,
                    expectedProviderApplicationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "KYC identity changed while the upload request was being prepared. Refresh and try again.");
            }

            var documentType = request.DocumentType.ToString();
            var document = application.Documents
                .FirstOrDefault(x => !x.IsDeleted && x.DocumentType == documentType);

            var now = DateTime.UtcNow;
            if (document is null)
            {
                document = new KycDocument
                {
                    KycApplicationId = application.Id,
                    KycApplication = application,
                    DocumentType = documentType,
                    CreatedByUserId = userId
                };

                application.Documents.Add(document);
            }

            document.FileName = request.FileName.Trim();
            document.MimeType = request.MimeType.Trim().ToLowerInvariant();
            document.StorageProvider = "BlaaizS3";
            document.StorageKey = result.ProviderFileId;
            document.StorageUrl = null;
            document.ProviderFileId = result.ProviderFileId;
            document.IsUploaded = false;
            document.UploadConfirmedAt = null;
            document.IsAttachedToProvider = false;
            document.AttachedToProviderAt = null;
            document.RejectionReason = null;
            document.UploadedAt = now;
            document.LastUpdatedAt = now;
            document.LastUpdatedByUserId = userId;

            if (application.Status == KycStatus.Rejected)
            {
                application.Status = KycStatus.Pending;
                application.ReviewNote = null;
                application.KycProfile.Status = KycStatus.Pending;
                application.KycProfile.RejectionReason = null;
                application.KycProfile.CustomerProfile.KycStatus = KycStatus.Pending;
            }

            try
            {
                await _db.SaveChangesAsync(ct);

                return new KycDocumentUploadUrlDto(
                    document.Id,
                    request.DocumentType,
                    result.ProviderFileId,
                    result.UploadUrl,
                    result.UploadHeaders);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // Re-query and retry local persistence once using the exact same
                // provider file ID and signed upload URL.
            }
        }

        throw new DbUpdateConcurrencyException(
            "KYC document upload state changed repeatedly while the request was being persisted.");
    }

    public async Task<KycDocumentDto> ConfirmDocumentUploadAsync(
        Guid userId,
        Guid applicationId,
        Guid documentId,
        ConfirmKycDocumentUploadRequestDto request,
        CancellationToken ct = default)
    {
        var providerFileId = request.ProviderFileId.Trim();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            _db.ChangeTracker.Clear();

            var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
            EnsureEditable(application);

            var document = application.Documents.FirstOrDefault(x =>
                x.Id == documentId &&
                !x.IsDeleted);

            if (document is null)
            {
                throw new InvalidOperationException("KYC document not found.");
            }

            if (!string.Equals(document.ProviderFileId, providerFileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The provider file ID does not match this upload request.");
            }

            var now = DateTime.UtcNow;
            document.IsUploaded = true;
            document.UploadConfirmedAt = now;
            document.UploadedAt = now;
            document.LastUpdatedAt = now;
            document.LastUpdatedByUserId = userId;

            try
            {
                await _db.SaveChangesAsync(ct);
                return ToDocumentDto(document);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // The provider upload already succeeded. Re-query and retry only
                // the idempotent local confirmation once.
            }
        }

        throw new DbUpdateConcurrencyException(
            "KYC document confirmation changed repeatedly while the request was being persisted.");
    }

    public async Task<KycApplicationDto> SubmitApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        if (string.IsNullOrWhiteSpace(application.ProviderApplicationId))
        {
            throw new InvalidOperationException("Provider customer has not been created for this KYC application.");
        }

        var identityFront = GetUploadedDocument(application, KycDocumentType.IdentityFront, true);
        var identityBack = GetUploadedDocument(application, KycDocumentType.IdentityBack, false);
        var proofOfAddress = GetUploadedDocument(application, KycDocumentType.ProofOfAddress, false);
        var liveness = GetUploadedDocument(application, KycDocumentType.LivenessCheck, false);

        if (application.IdentityType is "drivers_license" or "resident_permit" && identityBack is null)
        {
            throw new InvalidOperationException(
                "The back side of the identity document is required for this identification type.");
        }

        var result = await _provider.SubmitIndividualKycDocumentsAsync(
            new RemittanceKycDocumentSubmissionRequest(
                application.KycProfile.CustomerProfileId,
                application.ProviderApplicationId,
                identityFront!.ProviderFileId!,
                identityBack?.ProviderFileId,
                proofOfAddress?.ProviderFileId,
                liveness?.ProviderFileId),
            ct);

        var now = DateTime.UtcNow;
        foreach (var document in application.Documents.Where(x => !x.IsDeleted && x.IsUploaded))
        {
            document.IsAttachedToProvider = true;
            document.AttachedToProviderAt = now;
            document.LastUpdatedAt = now;
            document.LastUpdatedByUserId = userId;
        }

        application.Status = KycStatus.UnderReview;
        application.SubmittedAt = now;
        application.ProviderResponseJson = result.RawResponseJson;
        application.ReviewNote = null;
        application.LastUpdatedAt = now;
        application.LastUpdatedByUserId = userId;

        application.KycProfile.Status = KycStatus.UnderReview;
        application.KycProfile.SubmittedAt = now;
        application.KycProfile.RejectedAt = null;
        application.KycProfile.RejectionReason = null;
        application.KycProfile.LastUpdatedAt = now;
        application.KycProfile.LastUpdatedByUserId = userId;

        application.KycProfile.CustomerProfile.KycStatus = KycStatus.UnderReview;
        application.KycProfile.CustomerProfile.LastUpdatedAt = now;
        application.KycProfile.CustomerProfile.LastUpdatedByUserId = userId;

        var providerCustomer = await _db.ProviderCustomers.FirstOrDefaultAsync(x =>
            x.CustomerProfileId == application.KycProfile.CustomerProfileId &&
            x.ProviderCode == _provider.ProviderCode &&
            !x.IsDeleted,
            ct);

        if (providerCustomer is not null)
        {
            providerCustomer.ProviderStatus = result.ProviderStatus;
            providerCustomer.MetadataJson = result.RawResponseJson;
            providerCustomer.LastSyncedAt = now;
            providerCustomer.LastUpdatedAt = now;
            providerCustomer.LastUpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);
        return ToApplicationDto(application);
    }

    private async Task<KycApplication> GetOwnedApplicationAsync(
        Guid userId,
        Guid applicationId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<KycApplication> query = _db.KycApplications
            .Include(x => x.Documents)
            .Include(x => x.KycProfile)
            .ThenInclude(x => x.CustomerProfile)
            .ThenInclude(x => x.User);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var application = await query.FirstOrDefaultAsync(x =>
            x.Id == applicationId &&
            x.KycProfile.CustomerProfile.UserId == userId &&
            !x.IsDeleted &&
            !x.KycProfile.IsDeleted &&
            !x.KycProfile.CustomerProfile.IsDeleted,
            ct);

        return application ?? throw new InvalidOperationException("KYC application not found.");
    }

    private static void EnsureEditable(KycApplication application)
    {
        if (application.Status == KycStatus.Approved)
        {
            throw new InvalidOperationException("Approved KYC applications cannot be changed.");
        }

        if (application.Status == KycStatus.UnderReview)
        {
            throw new InvalidOperationException("KYC is currently under review and cannot be changed.");
        }

        if (application.Status == KycStatus.Expired)
        {
            throw new InvalidOperationException("This KYC application has expired. Start a new application.");
        }
    }

    private static void ValidateCustomerIdentityPrerequisites(CustomerProfile customer)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(customer.FirstName)) missing.Add("first name");
        if (string.IsNullOrWhiteSpace(customer.LastName)) missing.Add("last name");
        if (string.IsNullOrWhiteSpace(customer.Email) && string.IsNullOrWhiteSpace(customer.User.Email)) missing.Add("email");
        if (string.IsNullOrWhiteSpace(customer.CountryCode)) missing.Add("country");
        if (!customer.DateOfBirth.HasValue) missing.Add("date of birth");
        if (string.IsNullOrWhiteSpace(customer.AddressLine1)) missing.Add("address line 1");
        if (string.IsNullOrWhiteSpace(customer.City)) missing.Add("city");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Complete the customer profile before KYC. Missing: {string.Join(", ", missing)}.");
        }
    }

    private static KycDocument? GetUploadedDocument(
        KycApplication application,
        KycDocumentType documentType,
        bool required)
    {
        var document = application.Documents.FirstOrDefault(x =>
            !x.IsDeleted &&
            x.DocumentType == documentType.ToString() &&
            x.IsUploaded &&
            !string.IsNullOrWhiteSpace(x.ProviderFileId));

        if (required && document is null)
        {
            throw new InvalidOperationException($"{GetDocumentDisplayName(documentType)} is required before KYC submission.");
        }

        return document;
    }

    private static string GetDocumentDisplayName(KycDocumentType type) => type switch
    {
        KycDocumentType.IdentityFront => "Identity document",
        KycDocumentType.IdentityBack => "Identity document back",
        KycDocumentType.ProofOfAddress => "Proof of address",
        KycDocumentType.LivenessCheck => "Liveness check",
        _ => "KYC document"
    };

    private static KycStatus MapProviderStatus(string? providerStatus, KycStatus fallback)
    {
        return providerStatus?.Trim().ToUpperInvariant() switch
        {
            "VERIFIED" => KycStatus.Approved,
            "REJECTED" => KycStatus.Rejected,
            "PROCESSING" => KycStatus.UnderReview,
            "PENDING" => KycStatus.Pending,
            _ => fallback
        };
    }

    private static KycApplicationDto ToApplicationDto(KycApplication application)
    {
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
            application.Documents
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DocumentType)
                .Select(ToDocumentDto)
                .ToList(),
            application.CreatedAt,
            application.LastUpdatedAt);
    }

    private static KycDocumentDto ToDocumentDto(KycDocument document)
    {
        if (!Enum.TryParse<KycDocumentType>(document.DocumentType, out var documentType))
        {
            throw new InvalidOperationException($"Unsupported stored KYC document type '{document.DocumentType}'.");
        }

        return new KycDocumentDto(
            document.Id,
            documentType,
            document.FileName,
            document.MimeType,
            document.IsUploaded,
            document.UploadConfirmedAt,
            document.IsAttachedToProvider,
            document.AttachedToProviderAt,
            document.RejectionReason,
            document.CreatedAt);
    }
}
