using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Models.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Compliance;

public class BusinessKybService : IBusinessKybService
{
    private static readonly HashSet<string> AllowedMimeTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png"
    ];

    private static readonly HashSet<string> AllowedOwnerIdTypes =
    [
        "passport",
        "drivers_license",
        "resident_permit",
        "id_card"
    ];

    private static readonly HashSet<BusinessKybDocumentType> FormationDocumentTypes =
    [
        BusinessKybDocumentType.CertificateOfIncorporation,
        BusinessKybDocumentType.ArticlesOfIncorporation,
        BusinessKybDocumentType.BeneficialOwnershipCertificate,
        BusinessKybDocumentType.IncorporationDocuments,
        BusinessKybDocumentType.CacStatusReport
    ];

    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;
    private readonly IDataProtector _identityProtector;
    private readonly BlaaizOptions _blaaizOptions;
    private readonly IComplianceScreeningService _screeningService;

    public BusinessKybService(
        AppDbContext db,
        IRemittanceProvider provider,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<BlaaizOptions> blaaizOptions,
        IComplianceScreeningService screeningService)
    {
        _db = db;
        _provider = provider;
        _identityProtector = dataProtectionProvider.CreateProtector(
            "KorridorX.BusinessKyb.OwnerIdentity.v1");
        _blaaizOptions = blaaizOptions.Value;
        _screeningService = screeningService;
    }

    public async Task<BusinessKybApplicationDto> StartAsync(
        Guid userId,
        StartBusinessKybRequestDto request,
        CancellationToken ct = default)
    {
        if (request.KybScope == BusinessKybScope.Minimal && !_blaaizOptions.MinimalBusinessKybEnabled)
        {
            throw new InvalidOperationException(
                "MINIMAL business KYB is disabled. Use FULL KYB unless the provider has explicitly allow-listed this platform.");
        }

        var profile = await _db.BusinessProfiles
            .Include(x => x.Users)
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                (x.OwnerUserId == userId || x.Users.Any(u => u.UserId == userId && u.IsActive && !u.IsDeleted)),
                ct);

        if (profile is null)
        {
            profile = new BusinessProfile
            {
                OwnerUserId = userId,
                CreatedByUserId = userId
            };
            _db.BusinessProfiles.Add(profile);

            profile.Users.Add(new BusinessUser
            {
                BusinessProfileId = profile.Id,
                UserId = userId,
                Role = BusinessUserRole.Owner,
                IsActive = true,
                CreatedByUserId = userId
            });
        }

        var current = await _db.BusinessKybApplications
            .Include(x => x.Owners)
            .Include(x => x.Documents)
            .Where(x => x.BusinessProfileId == profile.Id && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (current is not null && current.Status is KybStatus.UnderReview or KybStatus.Approved)
        {
            return ToApplicationDto(current, profile);
        }

        ApplyProfile(profile, request);

        if (current is null || current.Status == KybStatus.Expired)
        {
            current = new BusinessKybApplication
            {
                BusinessProfileId = profile.Id,
                BusinessProfile = profile,
                Status = KybStatus.Pending,
                KybScope = request.KybScope,
                CreatedByUserId = userId
            };
            _db.BusinessKybApplications.Add(current);
        }
        else
        {
            current.KybScope = request.KybScope;
            current.Status = KybStatus.Pending;
            current.ReviewNote = null;
            current.LastUpdatedAt = DateTime.UtcNow;
            current.LastUpdatedByUserId = userId;
        }

        profile.KybScope = request.KybScope;
        profile.KybStatus = KybStatus.Pending;
        profile.KybRejectionReason = null;
        profile.LastUpdatedAt = DateTime.UtcNow;
        profile.LastUpdatedByUserId = userId;

        var providerResult = await SyncProviderAsync(profile, current, ct);
        current.ProviderApplicationId = providerResult.ProviderCustomerId;
        current.ProviderResponseJson = providerResult.RawResponseJson;

        await UpsertProviderCustomerAsync(profile, providerResult, userId, ct);
        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenBusinessAsync(
            profile.Id,
            ScreeningReason.Onboarding,
            userId,
            null,
            ct);
        await _db.SaveChangesAsync(ct);

        return ToApplicationDto(current, profile);
    }

    public async Task<BusinessKybStatusDto> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var profile = await GetOwnedBusinessProfileAsync(userId, false, ct);
        var application = await _db.BusinessKybApplications
            .AsNoTracking()
            .Include(x => x.Owners)
            .Include(x => x.Documents)
            .Where(x => x.BusinessProfileId == profile.Id && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new BusinessKybStatusDto(
            ToProfileDto(profile),
            application is null ? null : ToApplicationDto(application, profile));
    }

    public async Task<BusinessKybApplicationDto> GetApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, false, ct);
        return ToApplicationDto(application, application.BusinessProfile);
    }

    public async Task<BusinessKybApplicationDto> UpdateProfileAsync(
        Guid userId,
        Guid applicationId,
        UpdateBusinessKybProfileRequestDto request,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        ApplyProfile(application.BusinessProfile, request);
        application.Status = KybStatus.Pending;
        application.ReviewNote = null;
        application.LastUpdatedAt = DateTime.UtcNow;
        application.LastUpdatedByUserId = userId;

        var providerResult = await SyncProviderAsync(application.BusinessProfile, application, ct);
        ApplyProviderSnapshot(application, providerResult);
        await UpsertProviderCustomerAsync(application.BusinessProfile, providerResult, userId, ct);

        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenBusinessAsync(
            application.BusinessProfileId,
            ScreeningReason.ProfileChanged,
            userId,
            null,
            ct);
        await _db.SaveChangesAsync(ct);
        return ToApplicationDto(application, application.BusinessProfile);
    }

    public async Task<BusinessOwnerDto> AddOwnerAsync(
        Guid userId,
        Guid applicationId,
        CreateBusinessOwnerRequestDto request,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        if (application.KybScope != BusinessKybScope.Full)
        {
            throw new InvalidOperationException("Beneficial owners are managed only for FULL KYB applications.");
        }

        if (application.Owners.Count(x => !x.IsDeleted) >= 5)
        {
            throw new InvalidOperationException("A business KYB application can contain at most five owners.");
        }

        ValidateOwner(
            request.Email,
            request.DateOfBirth,
            request.OwnershipPercentage,
            request.IdDocumentType,
            request.IdDocumentNumber,
            request.IdDocumentCountry,
            request.IdExpiryDate);

        var email = request.Email.Trim().ToLowerInvariant();
        if (application.Owners.Any(x => !x.IsDeleted && x.Email.ToLower() == email))
        {
            throw new InvalidOperationException("Owner emails must be unique within the business.");
        }

        var activeOwnership = application.Owners
            .Where(x => !x.IsDeleted)
            .Sum(x => x.OwnershipPercentage);
        if (activeOwnership + request.OwnershipPercentage > 100m)
        {
            throw new InvalidOperationException("Owner ownership percentages cannot exceed 100%.");
        }

        var idNumber = request.IdDocumentNumber.Trim();
        var owner = new BusinessBeneficialOwner
        {
            BusinessKybApplicationId = application.Id,
            BusinessKybApplication = application,
            FirstName = Required(request.FirstName, "Owner first name"),
            LastName = Required(request.LastName, "Owner last name"),
            Email = email,
            DateOfBirth = request.DateOfBirth.Date,
            Nationality = NormalizeCode(request.Nationality),
            CountryCode = NormalizeCode(request.CountryCode),
            Title = Optional(request.Title),
            OwnershipPercentage = request.OwnershipPercentage,
            HasControl = request.HasControl,
            IsSigner = request.IsSigner,
            IsBeneficialOwner = request.IsBeneficialOwner,
            IsPep = request.IsPep,
            IdDocumentType = request.IdDocumentType.Trim().ToLowerInvariant(),
            IdentityNumberLastFour = LastFour(idNumber),
            IdentityNumberEncrypted = _identityProtector.Protect(idNumber),
            IdDocumentCountry = NormalizeCode(request.IdDocumentCountry),
            IdExpiryDate = request.IdExpiryDate.Date,
            ProviderStatus = "PENDING",
            CreatedByUserId = userId
        };

        application.Owners.Add(owner);
        application.Status = KybStatus.Pending;
        application.ReviewNote = null;

        var providerResult = await SyncProviderAsync(application.BusinessProfile, application, ct);
        ApplyProviderSnapshot(application, providerResult);
        await UpsertProviderCustomerAsync(application.BusinessProfile, providerResult, userId, ct);

        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenBusinessBeneficialOwnerAsync(
            owner.Id,
            ScreeningReason.Onboarding,
            userId,
            null,
            ct);
        await _db.SaveChangesAsync(ct);
        return ToOwnerDto(owner);
    }

    public async Task<BusinessOwnerDto> UpdateOwnerAsync(
        Guid userId,
        Guid applicationId,
        Guid ownerId,
        UpdateBusinessOwnerRequestDto request,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        var owner = application.Owners.FirstOrDefault(x => x.Id == ownerId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Business owner not found.");

        var idNumber = string.IsNullOrWhiteSpace(request.IdDocumentNumber)
            ? _identityProtector.Unprotect(owner.IdentityNumberEncrypted)
            : request.IdDocumentNumber.Trim();

        ValidateOwner(
            request.Email,
            request.DateOfBirth,
            request.OwnershipPercentage,
            request.IdDocumentType,
            idNumber,
            request.IdDocumentCountry,
            request.IdExpiryDate);

        var email = request.Email.Trim().ToLowerInvariant();
        if (application.Owners.Any(x =>
                !x.IsDeleted && x.Id != ownerId && x.Email.ToLower() == email))
        {
            throw new InvalidOperationException("Owner emails must be unique within the business.");
        }

        var otherOwnership = application.Owners
            .Where(x => !x.IsDeleted && x.Id != ownerId)
            .Sum(x => x.OwnershipPercentage);
        if (otherOwnership + request.OwnershipPercentage > 100m)
        {
            throw new InvalidOperationException("Owner ownership percentages cannot exceed 100%.");
        }

        owner.FirstName = Required(request.FirstName, "Owner first name");
        owner.LastName = Required(request.LastName, "Owner last name");
        owner.Email = email;
        owner.DateOfBirth = request.DateOfBirth.Date;
        owner.Nationality = NormalizeCode(request.Nationality);
        owner.CountryCode = NormalizeCode(request.CountryCode);
        owner.Title = Optional(request.Title);
        owner.OwnershipPercentage = request.OwnershipPercentage;
        owner.HasControl = request.HasControl;
        owner.IsSigner = request.IsSigner;
        owner.IsBeneficialOwner = request.IsBeneficialOwner;
        owner.IsPep = request.IsPep;
        owner.IdDocumentType = request.IdDocumentType.Trim().ToLowerInvariant();
        owner.IdentityNumberLastFour = LastFour(idNumber);
        owner.IdentityNumberEncrypted = _identityProtector.Protect(idNumber);
        owner.IdDocumentCountry = NormalizeCode(request.IdDocumentCountry);
        owner.IdExpiryDate = request.IdExpiryDate.Date;
        owner.ProviderStatus = "PENDING";
        owner.ProviderAdminCommentsJson = null;
        owner.RejectionReason = null;
        owner.LastUpdatedAt = DateTime.UtcNow;
        owner.LastUpdatedByUserId = userId;

        application.Status = KybStatus.Pending;
        application.ReviewNote = null;

        var providerResult = await SyncProviderAsync(application.BusinessProfile, application, ct);
        ApplyProviderSnapshot(application, providerResult);
        await UpsertProviderCustomerAsync(application.BusinessProfile, providerResult, userId, ct);

        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenBusinessBeneficialOwnerAsync(
            owner.Id,
            ScreeningReason.ProfileChanged,
            userId,
            null,
            ct);
        await _db.SaveChangesAsync(ct);
        return ToOwnerDto(owner);
    }

    public async Task<BusinessUploadUrlDto> RequestOwnerUploadUrlAsync(
        Guid userId,
        Guid applicationId,
        Guid ownerId,
        BusinessOwnerUploadUrlRequestDto request,
        CancellationToken ct = default)
    {
        ValidateFile(request.FileName, request.MimeType);
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        var owner = application.Owners.FirstOrDefault(x => x.Id == ownerId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Business owner not found.");

        if (string.IsNullOrWhiteSpace(application.ProviderApplicationId) ||
            string.IsNullOrWhiteSpace(owner.ProviderOwnerId))
        {
            var providerResult = await SyncProviderAsync(application.BusinessProfile, application, ct);
            ApplyProviderSnapshot(application, providerResult);
        }

        if (string.IsNullOrWhiteSpace(owner.ProviderOwnerId))
        {
            throw new InvalidOperationException("Provider owner could not be synchronized.");
        }

        var result = await _provider.RequestBusinessOwnerUploadUrlAsync(
            new RemittanceBusinessOwnerUploadUrlRequest(
                application.BusinessProfileId,
                application.ProviderApplicationId!,
                owner.ProviderOwnerId,
                request.Side),
            ct);

        if (request.Side == BusinessOwnerDocumentSide.Front)
        {
            owner.IdentityFrontProviderFileId = result.ProviderFileId;
            owner.IsIdentityFrontUploaded = false;
            owner.IdentityFrontUploadConfirmedAt = null;
        }
        else
        {
            owner.IdentityBackProviderFileId = result.ProviderFileId;
            owner.IsIdentityBackUploaded = false;
            owner.IdentityBackUploadConfirmedAt = null;
        }

        owner.AreIdentityFilesAttachedToProvider = false;
        owner.IdentityFilesAttachedAt = null;
        owner.LastUpdatedAt = DateTime.UtcNow;
        owner.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return new BusinessUploadUrlDto(
            owner.Id,
            result.ProviderFileId,
            result.UploadUrl,
            result.UploadHeaders);
    }

    public async Task<BusinessOwnerDto> ConfirmOwnerUploadAsync(
        Guid userId,
        Guid applicationId,
        Guid ownerId,
        ConfirmBusinessOwnerUploadRequestDto request,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        var owner = application.Owners.FirstOrDefault(x => x.Id == ownerId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Business owner not found.");

        var providerFileId = Required(request.ProviderFileId, "Provider file ID");
        var expected = request.Side == BusinessOwnerDocumentSide.Front
            ? owner.IdentityFrontProviderFileId
            : owner.IdentityBackProviderFileId;

        if (!string.Equals(expected, providerFileId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The provider file ID does not match this owner upload request.");
        }

        var front = request.Side == BusinessOwnerDocumentSide.Front ? providerFileId : null;
        var back = request.Side == BusinessOwnerDocumentSide.Back ? providerFileId : null;

        var result = await _provider.SubmitBusinessOwnerFilesAsync(
            new RemittanceBusinessOwnerFilesRequest(
                application.BusinessProfileId,
                Required(application.ProviderApplicationId, "Provider customer ID"),
                Required(owner.ProviderOwnerId, "Provider owner ID"),
                front,
                back),
            ct);

        var now = DateTime.UtcNow;
        if (request.Side == BusinessOwnerDocumentSide.Front)
        {
            owner.IsIdentityFrontUploaded = true;
            owner.IdentityFrontUploadConfirmedAt = now;
        }
        else
        {
            owner.IsIdentityBackUploaded = true;
            owner.IdentityBackUploadConfirmedAt = now;
        }

        owner.AreIdentityFilesAttachedToProvider =
            owner.IsIdentityFrontUploaded &&
            (owner.IdDocumentType == "passport" || owner.IsIdentityBackUploaded);
        owner.IdentityFilesAttachedAt = owner.AreIdentityFilesAttachedToProvider ? now : null;
        owner.ProviderStatus = result.ProviderStatus;
        owner.ProviderAdminCommentsJson = null;
        owner.RejectionReason = null;
        owner.LastUpdatedAt = now;
        owner.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return ToOwnerDto(owner);
    }

    public async Task<BusinessUploadUrlDto> RequestDocumentUploadUrlAsync(
        Guid userId,
        Guid applicationId,
        BusinessDocumentUploadUrlRequestDto request,
        CancellationToken ct = default)
    {
        ValidateFile(request.FileName, request.MimeType);
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        if (string.IsNullOrWhiteSpace(application.ProviderApplicationId))
        {
            var providerResult = await SyncProviderAsync(application.BusinessProfile, application, ct);
            ApplyProviderSnapshot(application, providerResult);
        }

        var result = await _provider.RequestBusinessDocumentUploadUrlAsync(
            new RemittanceBusinessDocumentUploadUrlRequest(
                application.BusinessProfileId,
                application.ProviderApplicationId!),
            ct);

        var document = new BusinessKybDocument
        {
            BusinessKybApplicationId = application.Id,
            BusinessKybApplication = application,
            DocumentType = request.DocumentType,
            Name = Required(request.Name, "Document name"),
            Description = Optional(request.Description),
            FileName = request.FileName.Trim(),
            MimeType = request.MimeType.Trim().ToLowerInvariant(),
            StorageProvider = "BlaaizS3",
            StorageKey = result.ProviderFileId,
            ProviderFileId = result.ProviderFileId,
            ProviderStatus = "PENDING",
            CreatedByUserId = userId
        };

        application.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        return new BusinessUploadUrlDto(
            document.Id,
            result.ProviderFileId,
            result.UploadUrl,
            result.UploadHeaders);
    }

    public async Task<BusinessKybDocumentDto> ConfirmDocumentUploadAsync(
        Guid userId,
        Guid applicationId,
        Guid documentId,
        ConfirmBusinessDocumentUploadRequestDto request,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        var document = application.Documents.FirstOrDefault(x => x.Id == documentId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Business KYB document not found.");

        var providerFileId = Required(request.ProviderFileId, "Provider file ID");
        if (!string.Equals(document.ProviderFileId, providerFileId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The provider file ID does not match this document upload request.");
        }

        var result = await _provider.RegisterBusinessDocumentAsync(
            new RemittanceBusinessDocumentRegistrationRequest(
                application.BusinessProfileId,
                Required(application.ProviderApplicationId, "Provider customer ID"),
                document.DocumentType,
                document.Name,
                providerFileId,
                document.Description),
            ct);

        var now = DateTime.UtcNow;
        document.IsUploaded = true;
        document.UploadConfirmedAt = now;
        document.IsRegisteredWithProvider = true;
        document.RegisteredWithProviderAt = now;
        document.ProviderDocumentId = result.ProviderDocumentId;
        document.ProviderStatus = result.ProviderStatus;
        document.ProviderAdminCommentsJson = result.AdminCommentsJson;
        document.RejectionReason = null;
        document.LastUpdatedAt = now;
        document.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return ToDocumentDto(document);
    }

    public async Task<BusinessKybApplicationDto> SubmitAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken ct = default)
    {
        var application = await GetOwnedApplicationAsync(userId, applicationId, true, ct);
        EnsureEditable(application);

        ValidateSubmissionReadiness(application);

        var providerResult = await SyncProviderAsync(application.BusinessProfile, application, ct);
        ApplyProviderSnapshot(application, providerResult);

        var submitted = await _provider.SubmitBusinessKybAsync(
            application.BusinessProfileId,
            Required(application.ProviderApplicationId, "Provider customer ID"),
            ct);

        var now = DateTime.UtcNow;
        application.Status = MapProviderStatus(submitted.ProviderStatus, KybStatus.UnderReview);
        application.SubmittedAt = now;
        application.ProviderResponseJson = submitted.RawResponseJson;
        application.SubmittedPayloadJson = CreateRedactedSubmissionSnapshot(application);
        application.ReviewNote = null;
        application.LastUpdatedAt = now;
        application.LastUpdatedByUserId = userId;

        application.BusinessProfile.KybStatus = application.Status;
        application.BusinessProfile.KybSubmittedAt = now;
        application.BusinessProfile.KybRejectedAt = null;
        application.BusinessProfile.KybRejectionReason = null;
        application.BusinessProfile.LastUpdatedAt = now;
        application.BusinessProfile.LastUpdatedByUserId = userId;

        foreach (var owner in application.Owners.Where(x => !x.IsDeleted))
        {
            if (owner.ProviderStatus == "PENDING") owner.ProviderStatus = "PROCESSING";
        }

        foreach (var document in application.Documents.Where(x => !x.IsDeleted))
        {
            if (document.ProviderStatus == "PENDING") document.ProviderStatus = "PROCESSING";
        }

        var providerCustomer = await _db.ProviderCustomers.FirstOrDefaultAsync(x =>
            x.BusinessProfileId == application.BusinessProfileId &&
            x.ProviderCode == _provider.ProviderCode &&
            !x.IsDeleted,
            ct);

        if (providerCustomer is not null)
        {
            providerCustomer.ProviderStatus = submitted.ProviderStatus;
            providerCustomer.MetadataJson = submitted.RawResponseJson;
            providerCustomer.LastSyncedAt = now;
            providerCustomer.LastUpdatedAt = now;
            providerCustomer.LastUpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);
        return ToApplicationDto(application, application.BusinessProfile);
    }

    private async Task<BusinessKybApplication> GetOwnedApplicationAsync(
        Guid userId,
        Guid applicationId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<BusinessKybApplication> query = _db.BusinessKybApplications
            .Include(x => x.BusinessProfile)
            .ThenInclude(x => x.Users)
            .Include(x => x.Owners)
            .Include(x => x.Documents);

        if (!tracking) query = query.AsNoTracking();

        var application = await query.FirstOrDefaultAsync(x =>
            x.Id == applicationId &&
            !x.IsDeleted &&
            !x.BusinessProfile.IsDeleted &&
            (x.BusinessProfile.OwnerUserId == userId ||
             x.BusinessProfile.Users.Any(u => u.UserId == userId && u.IsActive && !u.IsDeleted)),
            ct);

        return application ?? throw new InvalidOperationException("Business KYB application not found.");
    }

    private async Task<BusinessProfile> GetOwnedBusinessProfileAsync(
        Guid userId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<BusinessProfile> query = _db.BusinessProfiles.Include(x => x.Users);
        if (!tracking) query = query.AsNoTracking();

        var profile = await query.FirstOrDefaultAsync(x =>
            !x.IsDeleted &&
            (x.OwnerUserId == userId || x.Users.Any(u => u.UserId == userId && u.IsActive && !u.IsDeleted)),
            ct);

        return profile ?? throw new InvalidOperationException("Business profile not found.");
    }

    private async Task<RemittanceBusinessCustomerResult> SyncProviderAsync(
        BusinessProfile profile,
        BusinessKybApplication application,
        CancellationToken ct)
    {
        ValidateProfile(profile);

        var owners = application.Owners
            .Where(x => !x.IsDeleted)
            .Select(x => new RemittanceBusinessOwnerRequest(
                x.Id,
                x.ProviderOwnerId,
                x.FirstName,
                x.LastName,
                x.Email,
                x.DateOfBirth,
                x.Nationality,
                x.CountryCode,
                x.Title,
                x.OwnershipPercentage,
                x.HasControl,
                x.IsSigner,
                x.IsBeneficialOwner,
                x.IdDocumentType,
                _identityProtector.Unprotect(x.IdentityNumberEncrypted),
                x.IdDocumentCountry,
                x.IdExpiryDate,
                x.IsPep))
            .ToList();

        return await _provider.SyncBusinessCustomerAsync(
            new RemittanceBusinessCustomerRequest(
                profile.Id,
                profile.BlaaizBusinessCustomerId,
                profile.BusinessName,
                profile.TradingName,
                profile.BusinessType,
                Required(profile.RegistrationNumber, "Registration number"),
                profile.CountryCode,
                profile.IncorporationDate,
                profile.IndustryType,
                profile.BusinessDescription,
                profile.Website,
                profile.SourceOfFunds,
                profile.EstimatedAnnualRevenue,
                profile.ExpectedMonthlyPayments,
                profile.AccountPurpose,
                profile.KybScope == BusinessKybScope.Minimal ? "MINIMAL" : "FULL",
                Required(profile.ContactEmail, "Business email"),
                profile.ContactPhone,
                profile.TaxIdentificationNumber,
                profile.CountryCode,
                profile.AddressLine1,
                profile.City,
                profile.StateOrProvince,
                profile.PostalCode,
                profile.OperatingCountryCode,
                profile.OperatingAddressLine1,
                profile.OperatingCity,
                profile.OperatingStateOrProvince,
                profile.OperatingPostalCode,
                owners),
            ct);
    }

    private static void ApplyProviderSnapshot(
        BusinessKybApplication application,
        RemittanceBusinessCustomerResult result)
    {
        application.ProviderApplicationId = result.ProviderCustomerId;
        application.ProviderResponseJson = result.RawResponseJson;
        application.BusinessProfile.BlaaizBusinessCustomerId = result.ProviderCustomerId;

        foreach (var mappedOwner in result.Owners)
        {
            var owner = mappedOwner.LocalOwnerId != Guid.Empty
                ? application.Owners.FirstOrDefault(x => x.Id == mappedOwner.LocalOwnerId)
                : application.Owners.FirstOrDefault(x => x.ProviderOwnerId == mappedOwner.ProviderOwnerId);

            if (owner is null) continue;
            owner.ProviderOwnerId = mappedOwner.ProviderOwnerId;
            owner.ProviderStatus = mappedOwner.ProviderStatus;
            owner.ProviderAdminCommentsJson = mappedOwner.AdminCommentsJson;
        }

        foreach (var mappedDocument in result.Documents)
        {
            var document = application.Documents.FirstOrDefault(x =>
                x.ProviderDocumentId == mappedDocument.ProviderDocumentId ||
                (x.Name == mappedDocument.Name && MapDocumentType(x.DocumentType) == mappedDocument.DocumentType));

            if (document is null) continue;
            document.ProviderDocumentId = mappedDocument.ProviderDocumentId;
            document.ProviderStatus = mappedDocument.ProviderStatus;
            document.ProviderAdminCommentsJson = mappedDocument.AdminCommentsJson;
        }
    }

    private async Task UpsertProviderCustomerAsync(
        BusinessProfile profile,
        RemittanceBusinessCustomerResult result,
        Guid userId,
        CancellationToken ct)
    {
        var providerCustomer = await _db.ProviderCustomers.FirstOrDefaultAsync(x =>
            x.BusinessProfileId == profile.Id &&
            x.ProviderCode == _provider.ProviderCode &&
            !x.IsDeleted,
            ct);

        if (providerCustomer is null)
        {
            providerCustomer = new ProviderCustomer
            {
                BusinessProfileId = profile.Id,
                BusinessProfile = profile,
                ProviderCode = _provider.ProviderCode,
                ProviderCustomerId = result.ProviderCustomerId,
                CreatedByUserId = userId
            };
            _db.ProviderCustomers.Add(providerCustomer);
        }

        providerCustomer.ProviderCustomerId = result.ProviderCustomerId;
        providerCustomer.ProviderStatus = result.ProviderStatus;
        providerCustomer.MetadataJson = result.RawResponseJson;
        providerCustomer.LastSyncedAt = DateTime.UtcNow;
        providerCustomer.LastUpdatedAt = DateTime.UtcNow;
        providerCustomer.LastUpdatedByUserId = userId;
        profile.BlaaizBusinessCustomerId = result.ProviderCustomerId;
    }

    private static void EnsureEditable(BusinessKybApplication application)
    {
        if (application.Status == KybStatus.Approved)
        {
            throw new InvalidOperationException(
                "Verified business records are locked. Contact operations for a correction request.");
        }

        if (application.Status == KybStatus.UnderReview)
        {
            throw new InvalidOperationException("Business KYB is under review and cannot be changed.");
        }

        if (application.Status == KybStatus.Expired)
        {
            throw new InvalidOperationException("This business KYB application has expired.");
        }
    }

    private static void ValidateSubmissionReadiness(BusinessKybApplication application)
    {
        var formationDocumentExists = application.Documents.Any(x =>
            !x.IsDeleted &&
            FormationDocumentTypes.Contains(x.DocumentType) &&
            x.IsUploaded &&
            x.IsRegisteredWithProvider);

        if (!formationDocumentExists)
        {
            throw new InvalidOperationException("At least one registered formation document is required.");
        }

        if (application.KybScope == BusinessKybScope.Minimal) return;

        var owners = application.Owners.Where(x => !x.IsDeleted).ToList();
        if (owners.Count == 0)
        {
            throw new InvalidOperationException("At least one beneficial owner is required for FULL KYB.");
        }

        if (owners.Sum(x => x.OwnershipPercentage) != 100m)
        {
            throw new InvalidOperationException("Owner ownership percentages must sum to exactly 100%.");
        }

        if (owners.Select(x => x.Email.ToLowerInvariant()).Distinct().Count() != owners.Count)
        {
            throw new InvalidOperationException("Owner emails must be unique within the business.");
        }

        foreach (var owner in owners)
        {
            if (!owner.IsIdentityFrontUploaded)
            {
                throw new InvalidOperationException($"Identity front is missing for {owner.FirstName} {owner.LastName}.");
            }

            if (owner.IdDocumentType == "passport" && owner.IsIdentityBackUploaded)
            {
                throw new InvalidOperationException(
                    $"Passport owner {owner.FirstName} {owner.LastName} must not have an identity back file.");
            }

            if (owner.IdDocumentType != "passport" && !owner.IsIdentityBackUploaded)
            {
                throw new InvalidOperationException(
                    $"Identity back is required for {owner.FirstName} {owner.LastName}.");
            }
        }
    }

    private static void ValidateProfile(BusinessProfile profile)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.BusinessName)) missing.Add("business name");
        if (string.IsNullOrWhiteSpace(profile.RegistrationNumber)) missing.Add("registration number");
        if (string.IsNullOrWhiteSpace(profile.CountryCode)) missing.Add("incorporation country");
        if (string.IsNullOrWhiteSpace(profile.ContactEmail)) missing.Add("contact email");
        if (string.IsNullOrWhiteSpace(profile.AddressLine1)) missing.Add("registered address");
        if (string.IsNullOrWhiteSpace(profile.City)) missing.Add("registered city");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Complete the business profile before provider synchronization. Missing: {string.Join(", ", missing)}.");
        }

        if (!string.IsNullOrWhiteSpace(profile.OperatingAddressLine1) &&
            string.IsNullOrWhiteSpace(profile.OperatingCountryCode))
        {
            throw new InvalidOperationException(
                "Operating country is required when an operating address is supplied.");
        }
    }

    private static void ValidateOwner(
        string email,
        DateTime dateOfBirth,
        decimal ownershipPercentage,
        string idDocumentType,
        string idDocumentNumber,
        string idDocumentCountry,
        DateTime idExpiryDate)
    {
        _ = Required(email, "Owner email");
        _ = Required(idDocumentNumber, "Owner identity document number");
        _ = NormalizeCode(idDocumentCountry);

        var type = Required(idDocumentType, "Owner identity document type").ToLowerInvariant();
        if (!AllowedOwnerIdTypes.Contains(type))
        {
            throw new InvalidOperationException(
                "Owner identity document type must be passport, drivers_license, resident_permit, or id_card.");
        }

        if (dateOfBirth.Date > DateTime.UtcNow.Date.AddYears(-18))
        {
            throw new InvalidOperationException("Every business owner must be at least 18 years old.");
        }

        if (idExpiryDate.Date <= DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException("Owner identity document expiry date must be in the future.");
        }

        if (ownershipPercentage <= 0m || ownershipPercentage > 100m)
        {
            throw new InvalidOperationException("Ownership percentage must be greater than zero and at most 100.");
        }
    }

    private static void ValidateFile(string fileName, string mimeType)
    {
        _ = Required(fileName, "File name");
        var normalizedMime = Required(mimeType, "MIME type").ToLowerInvariant();
        if (!AllowedMimeTypes.Contains(normalizedMime))
        {
            throw new InvalidOperationException("Allowed files are PDF, JPEG, and PNG.");
        }
    }

    private static void ApplyProfile(BusinessProfile profile, StartBusinessKybRequestDto request)
    {
        profile.KybScope = request.KybScope;
        ApplyProfileValues(
            profile,
            request.BusinessName,
            request.TradingName,
            request.BusinessType,
            request.RegistrationNumber,
            request.TaxIdentificationNumber,
            request.CountryCode,
            request.IncorporationDate,
            request.IndustryType,
            request.BusinessDescription,
            request.Website,
            request.SourceOfFunds,
            request.EstimatedAnnualRevenue,
            request.ExpectedMonthlyPayments,
            request.AccountPurpose,
            request.StateOrProvince,
            request.City,
            request.AddressLine1,
            request.AddressLine2,
            request.PostalCode,
            request.OperatingCountryCode,
            request.OperatingStateOrProvince,
            request.OperatingCity,
            request.OperatingAddressLine1,
            request.OperatingAddressLine2,
            request.OperatingPostalCode,
            request.ContactEmail,
            request.ContactPhone);
    }

    private static void ApplyProfile(BusinessProfile profile, UpdateBusinessKybProfileRequestDto request) =>
        ApplyProfileValues(
            profile,
            request.BusinessName,
            request.TradingName,
            request.BusinessType,
            request.RegistrationNumber,
            request.TaxIdentificationNumber,
            request.CountryCode,
            request.IncorporationDate,
            request.IndustryType,
            request.BusinessDescription,
            request.Website,
            request.SourceOfFunds,
            request.EstimatedAnnualRevenue,
            request.ExpectedMonthlyPayments,
            request.AccountPurpose,
            request.StateOrProvince,
            request.City,
            request.AddressLine1,
            request.AddressLine2,
            request.PostalCode,
            request.OperatingCountryCode,
            request.OperatingStateOrProvince,
            request.OperatingCity,
            request.OperatingAddressLine1,
            request.OperatingAddressLine2,
            request.OperatingPostalCode,
            request.ContactEmail,
            request.ContactPhone);

    private static void ApplyProfileValues(
        BusinessProfile profile,
        string businessName,
        string? tradingName,
        string? businessType,
        string registrationNumber,
        string? taxIdentificationNumber,
        string countryCode,
        DateTime? incorporationDate,
        string? industryType,
        string? businessDescription,
        string? website,
        string? sourceOfFunds,
        string? estimatedAnnualRevenue,
        int? expectedMonthlyPayments,
        string? accountPurpose,
        string? stateOrProvince,
        string? city,
        string? addressLine1,
        string? addressLine2,
        string? postalCode,
        string? operatingCountryCode,
        string? operatingStateOrProvince,
        string? operatingCity,
        string? operatingAddressLine1,
        string? operatingAddressLine2,
        string? operatingPostalCode,
        string contactEmail,
        string? contactPhone)
    {
        profile.BusinessName = Required(businessName, "Business name");
        profile.TradingName = Optional(tradingName);
        profile.BusinessType = Optional(businessType);
        profile.RegistrationNumber = Required(registrationNumber, "Registration number");
        profile.TaxIdentificationNumber = Optional(taxIdentificationNumber);
        profile.CountryCode = NormalizeCode(countryCode);
        profile.IncorporationDate = incorporationDate?.Date;
        profile.IndustryType = Optional(industryType);
        profile.BusinessDescription = Optional(businessDescription);
        profile.Website = Optional(website);
        profile.SourceOfFunds = Optional(sourceOfFunds);
        profile.EstimatedAnnualRevenue = Optional(estimatedAnnualRevenue);
        profile.ExpectedMonthlyPayments = expectedMonthlyPayments;
        profile.AccountPurpose = Optional(accountPurpose);
        profile.StateOrProvince = Optional(stateOrProvince);
        profile.City = Optional(city);
        profile.AddressLine1 = Optional(addressLine1);
        profile.AddressLine2 = Optional(addressLine2);
        profile.PostalCode = Optional(postalCode);
        profile.OperatingCountryCode = string.IsNullOrWhiteSpace(operatingCountryCode)
            ? null
            : NormalizeCode(operatingCountryCode);
        profile.OperatingStateOrProvince = Optional(operatingStateOrProvince);
        profile.OperatingCity = Optional(operatingCity);
        profile.OperatingAddressLine1 = Optional(operatingAddressLine1);
        profile.OperatingAddressLine2 = Optional(operatingAddressLine2);
        profile.OperatingPostalCode = Optional(operatingPostalCode);
        profile.ContactEmail = Required(contactEmail, "Business email").ToLowerInvariant();
        profile.ContactPhone = Optional(contactPhone);
    }

    private static string CreateRedactedSubmissionSnapshot(BusinessKybApplication application) =>
        JsonSerializer.Serialize(new
        {
            application.BusinessProfileId,
            application.KybScope,
            application.BusinessProfile.BusinessName,
            application.BusinessProfile.RegistrationNumber,
            application.BusinessProfile.CountryCode,
            owners = application.Owners.Where(x => !x.IsDeleted).Select(x => new
            {
                x.Id,
                x.ProviderOwnerId,
                x.FirstName,
                x.LastName,
                x.Email,
                x.OwnershipPercentage,
                x.IdDocumentType,
                x.IdentityNumberLastFour,
                x.ProviderStatus
            }),
            documents = application.Documents.Where(x => !x.IsDeleted).Select(x => new
            {
                x.Id,
                x.ProviderDocumentId,
                x.DocumentType,
                x.Name,
                x.ProviderStatus
            })
        });

    private static BusinessKybApplicationDto ToApplicationDto(
        BusinessKybApplication application,
        BusinessProfile profile) =>
        new(
            application.Id,
            application.BusinessProfileId,
            application.Status,
            application.KybScope,
            application.ProviderApplicationId,
            application.SubmittedAt,
            application.ReviewedAt,
            application.ReviewNote,
            application.CreatedAt,
            ToProfileDto(profile),
            application.Owners.Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAt).Select(ToOwnerDto).ToList(),
            application.Documents.Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAt).Select(ToDocumentDto).ToList());

    private static BusinessProfileKybDto ToProfileDto(BusinessProfile profile) =>
        new(
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

    private static BusinessOwnerDto ToOwnerDto(BusinessBeneficialOwner owner) =>
        new(
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

    private static BusinessKybDocumentDto ToDocumentDto(BusinessKybDocument document) =>
        new(
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

    private static KybStatus MapProviderStatus(string? status, KybStatus fallback) =>
        status?.Trim().ToUpperInvariant() switch
        {
            "VERIFIED" => KybStatus.Approved,
            "REJECTED" => KybStatus.Rejected,
            "PROCESSING" => KybStatus.UnderReview,
            "PENDING" => KybStatus.Pending,
            _ => fallback
        };

    private static string MapDocumentType(BusinessKybDocumentType type) => type switch
    {
        BusinessKybDocumentType.CertificateOfIncorporation => "CERTIFICATE_OF_INCORPORATION",
        BusinessKybDocumentType.ArticlesOfIncorporation => "ARTICLES_OF_INCORPORATION",
        BusinessKybDocumentType.BeneficialOwnershipCertificate => "BENEFICIAL_OWNERSHIP_CERTIFICATE",
        BusinessKybDocumentType.IncorporationDocuments => "INCORPORATION_DOCUMENTS",
        BusinessKybDocumentType.CacStatusReport => "CAC_STATUS_REPORT",
        BusinessKybDocumentType.ShareRegister => "SHARE_REGISTER",
        BusinessKybDocumentType.BankStatement => "BANK_STATEMENT",
        BusinessKybDocumentType.ProofOfBusinessAddress => "PROOF_OF_ADDRESS",
        BusinessKybDocumentType.TaxDocument => "TAX_DOCUMENT",
        _ => "OTHER"
    };

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string NormalizeCode(string value)
    {
        var code = Required(value, "Code").ToUpperInvariant();
        if (code.Length != 2)
        {
            throw new InvalidOperationException("Country codes must use ISO alpha-2 format.");
        }
        return code;
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string LastFour(string value) =>
        value.Length <= 4 ? value : value[^4..];
}
