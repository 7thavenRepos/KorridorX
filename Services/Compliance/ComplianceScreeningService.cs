using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Dtos.Audit;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Providers.Screening;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Compliance;

public sealed class ComplianceScreeningService : IComplianceScreeningService
{
    private readonly AppDbContext _db;
    private readonly ISanctionsScreeningProvider _provider;
    private readonly IComplianceCaseService _caseService;
    private readonly IAuditService _audit;
    private readonly ComplianceScreeningOptions _options;

    public ComplianceScreeningService(
        AppDbContext db,
        ISanctionsScreeningProvider provider,
        IComplianceCaseService caseService,
        IAuditService audit,
        IOptions<ComplianceScreeningOptions> options)
    {
        _db = db;
        _provider = provider;
        _caseService = caseService;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<ScreeningRecordDto?> ScreenCustomerAsync(
        Guid customerProfileId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default)
    {
        var entity = await _db.CustomerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerProfileId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Customer profile not found for screening.");
        var subject = new ScreeningSubjectSnapshot(
            ScreeningSubjectType.Customer,
            entity.Id,
            BuildName(entity.FirstName, entity.MiddleName, entity.LastName),
            Array.Empty<string>(),
            entity.CountryCode,
            entity.DateOfBirth,
            null,
            entity.Id,
            null,
            null,
            null);
        return await ScreenSubjectAsync(subject, reason, initiatedByUserId, transferId, ct);
    }

    public async Task<ScreeningRecordDto?> ScreenBusinessAsync(
        Guid businessProfileId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default)
    {
        var entity = await _db.BusinessProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessProfileId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business profile not found for screening.");
        var aliases = string.IsNullOrWhiteSpace(entity.TradingName)
            ? Array.Empty<string>()
            : new[] { entity.TradingName! };
        var subject = new ScreeningSubjectSnapshot(
            ScreeningSubjectType.Business,
            entity.Id,
            entity.BusinessName,
            aliases,
            entity.CountryCode,
            null,
            LastFour(entity.RegistrationNumber),
            null,
            entity.Id,
            null,
            null);
        return await ScreenSubjectAsync(subject, reason, initiatedByUserId, transferId, ct);
    }

    public async Task<ScreeningRecordDto?> ScreenRecipientAsync(
        Guid recipientId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default)
    {
        var entity = await _db.Recipients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Recipient not found for screening.");
        var aliases = string.IsNullOrWhiteSpace(entity.Nickname)
            ? Array.Empty<string>()
            : new[] { entity.Nickname! };
        var subject = new ScreeningSubjectSnapshot(
            ScreeningSubjectType.Recipient,
            entity.Id,
            BuildName(entity.FirstName, entity.MiddleName, entity.LastName),
            aliases,
            entity.CountryCode,
            null,
            null,
            entity.CustomerProfileId,
            null,
            entity.Id,
            null);
        return await ScreenSubjectAsync(subject, reason, initiatedByUserId, transferId, ct);
    }

    public async Task<ScreeningRecordDto?> ScreenBusinessBeneficiaryAsync(
        Guid businessBeneficiaryId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default)
    {
        var entity = await _db.BusinessBeneficiaries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessBeneficiaryId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found for screening.");
        var aliases = string.IsNullOrWhiteSpace(entity.Nickname)
            ? Array.Empty<string>()
            : new[] { entity.Nickname! };
        var subject = new ScreeningSubjectSnapshot(
            ScreeningSubjectType.BusinessBeneficiary,
            entity.Id,
            entity.Name,
            aliases,
            entity.CountryCode,
            null,
            null,
            null,
            entity.BusinessProfileId,
            null,
            entity.Id);
        return await ScreenSubjectAsync(subject, reason, initiatedByUserId, transferId, ct);
    }

    public async Task<ScreeningRecordDto?> ScreenBusinessBeneficialOwnerAsync(
        Guid businessBeneficialOwnerId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default)
    {
        var entity = await _db.BusinessBeneficialOwners.AsNoTracking()
            .Include(x => x.BusinessKybApplication)
            .FirstOrDefaultAsync(x => x.Id == businessBeneficialOwnerId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business beneficial owner not found for screening.");
        var subject = new ScreeningSubjectSnapshot(
            ScreeningSubjectType.BusinessBeneficialOwner,
            entity.Id,
            BuildName(entity.FirstName, null, entity.LastName),
            Array.Empty<string>(),
            entity.CountryCode,
            entity.DateOfBirth,
            entity.IdentityNumberLastFour,
            null,
            entity.BusinessKybApplication.BusinessProfileId,
            null,
            null,
            entity.Id,
            entity.IsPep);
        return await ScreenSubjectAsync(subject, reason, initiatedByUserId, transferId, ct);
    }

    public async Task<IReadOnlyList<ScreeningRecordDto>> ScreenTransferAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        var results = new List<ScreeningRecordDto>();
        if (transfer.CustomerProfileId.HasValue)
        {
            var sender = await ScreenCustomerAsync(
                transfer.CustomerProfileId.Value,
                ScreeningReason.TransferCreated,
                initiatedByUserId,
                transfer.Id,
                ct);
            if (sender is not null) results.Add(sender);
        }
        if (transfer.BusinessProfileId.HasValue)
        {
            var sender = await ScreenBusinessAsync(
                transfer.BusinessProfileId.Value,
                ScreeningReason.TransferCreated,
                initiatedByUserId,
                transfer.Id,
                ct);
            if (sender is not null) results.Add(sender);

            var ownerIds = await _db.BusinessBeneficialOwners.AsNoTracking()
                .Where(x =>
                    !x.IsDeleted &&
                    x.BusinessKybApplication.BusinessProfileId == transfer.BusinessProfileId.Value)
                .Select(x => x.Id)
                .ToListAsync(ct);
            foreach (var ownerId in ownerIds)
            {
                var owner = await ScreenBusinessBeneficialOwnerAsync(
                    ownerId,
                    ScreeningReason.TransferCreated,
                    initiatedByUserId,
                    transfer.Id,
                    ct);
                if (owner is not null) results.Add(owner);
            }
        }
        if (transfer.RecipientId.HasValue)
        {
            var recipient = await ScreenRecipientAsync(
                transfer.RecipientId.Value,
                ScreeningReason.TransferCreated,
                initiatedByUserId,
                transfer.Id,
                ct);
            if (recipient is not null) results.Add(recipient);
        }
        if (transfer.BusinessBeneficiaryId.HasValue)
        {
            var beneficiary = await ScreenBusinessBeneficiaryAsync(
                transfer.BusinessBeneficiaryId.Value,
                ScreeningReason.TransferCreated,
                initiatedByUserId,
                transfer.Id,
                ct);
            if (beneficiary is not null) results.Add(beneficiary);
        }

        var hasBlockingCase = await _caseService.HasBlockingCaseAsync(
            transfer.Id,
            transfer.CustomerProfileId,
            transfer.BusinessProfileId,
            transfer.RecipientId,
            transfer.BusinessBeneficiaryId,
            ct);

        if (results.Any(x => x.IsBlocking) || hasBlockingCase)
        {
            transfer.IsComplianceHold = true;
            transfer.ComplianceHoldReason = "A sanctions, PEP, or watchlist screening requires compliance review.";
            transfer.RiskDecision = RiskDecision.Block;
            transfer.RiskLevel = RiskLevel.Critical;
            transfer.LastUpdatedAt = DateTime.UtcNow;
            _db.TransferTimelineEvents.Add(new TransferTimelineEvent
            {
                TransferId = transfer.Id,
                EventType = "WATCHLIST_SCREENING_HOLD",
                Title = "Compliance screening review required",
                Description = "The transfer is on hold while a watchlist screening result is reviewed.",
                OccurredAt = DateTime.UtcNow
            });
        }

        return results;
    }

    public async Task<ScreeningRecordDto> ManualScreenAsync(
        ScreeningSubjectType subjectType,
        Guid subjectId,
        Guid? transferId,
        Guid initiatedByUserId,
        CancellationToken ct = default)
    {
        if (!_options.IsEnabled)
            throw new InvalidOperationException("Compliance screening is currently disabled.");

        if (transferId.HasValue)
            await EnsureTransferSubjectLinkAsync(subjectType, subjectId, transferId.Value, ct);

        var result = subjectType switch
        {
            ScreeningSubjectType.Customer => await ScreenCustomerAsync(subjectId, ScreeningReason.Manual, initiatedByUserId, transferId, ct),
            ScreeningSubjectType.Business => await ScreenBusinessAsync(subjectId, ScreeningReason.Manual, initiatedByUserId, transferId, ct),
            ScreeningSubjectType.Recipient => await ScreenRecipientAsync(subjectId, ScreeningReason.Manual, initiatedByUserId, transferId, ct),
            ScreeningSubjectType.BusinessBeneficiary => await ScreenBusinessBeneficiaryAsync(subjectId, ScreeningReason.Manual, initiatedByUserId, transferId, ct),
            ScreeningSubjectType.BusinessBeneficialOwner => await ScreenBusinessBeneficialOwnerAsync(subjectId, ScreeningReason.Manual, initiatedByUserId, transferId, ct),
            _ => throw new InvalidOperationException("Manual screening supports customers, businesses, recipients, business beneficiaries, and beneficial owners.")
        };

        var resolved = result ?? throw new InvalidOperationException("Screening did not produce a result.");
        _audit.Stage(new AuditRecordRequest(
            "MANUAL_COMPLIANCE_SCREENING_RUN",
            "Compliance",
            nameof(ScreeningRecord),
            resolved.Id.ToString(),
            null,
            new
            {
                subjectType,
                subjectId,
                transferId,
                resolved.Status,
                resolved.HighestMatchScore,
                resolved.IsBlocking,
                resolved.ProviderCode
            },
            null,
            initiatedByUserId));
        await _db.SaveChangesAsync(ct);
        return resolved;
    }

    public async Task EnsureTransferCanProceedToPayoutAsync(
        Transfer transfer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        if (await _caseService.HasBlockingCaseAsync(
                transfer.Id,
                transfer.CustomerProfileId,
                transfer.BusinessProfileId,
                transfer.RecipientId,
                transfer.BusinessBeneficiaryId,
                ct))
        {
            throw new ComplianceHoldException(
                "This transfer has an unresolved blocking sanctions, PEP, or transaction-monitoring case.");
        }

        if (!_options.IsEnabled)
        {
            if (_options.RequireRecentClearScreeningForPayout)
                throw new ComplianceHoldException("Payout screening is required but compliance screening is disabled.");
            return;
        }

        if (!_options.RequireRecentClearScreeningForPayout)
            return;

        var now = DateTime.UtcNow;
        var requiredSubjects = new List<(ScreeningSubjectType Type, Guid Id)>();
        if (transfer.CustomerProfileId.HasValue) requiredSubjects.Add((ScreeningSubjectType.Customer, transfer.CustomerProfileId.Value));
        if (transfer.BusinessProfileId.HasValue) requiredSubjects.Add((ScreeningSubjectType.Business, transfer.BusinessProfileId.Value));
        if (transfer.RecipientId.HasValue) requiredSubjects.Add((ScreeningSubjectType.Recipient, transfer.RecipientId.Value));
        if (transfer.BusinessBeneficiaryId.HasValue) requiredSubjects.Add((ScreeningSubjectType.BusinessBeneficiary, transfer.BusinessBeneficiaryId.Value));

        foreach (var subject in requiredSubjects)
        {
            var clearQuery = _db.ScreeningRecords.AsNoTracking().Where(x =>
                !x.IsDeleted &&
                x.SubjectType == subject.Type &&
                x.Status == ScreeningStatus.Clear &&
                x.ExpiresAt.HasValue && x.ExpiresAt.Value > now);
            clearQuery = subject.Type switch
            {
                ScreeningSubjectType.Customer => clearQuery.Where(x => x.CustomerProfileId == subject.Id),
                ScreeningSubjectType.Business => clearQuery.Where(x => x.BusinessProfileId == subject.Id),
                ScreeningSubjectType.Recipient => clearQuery.Where(x => x.RecipientId == subject.Id),
                ScreeningSubjectType.BusinessBeneficiary => clearQuery.Where(x => x.BusinessBeneficiaryId == subject.Id),
                _ => clearQuery.Where(x => false)
            };
            var clear = await clearQuery.AnyAsync(ct);
            if (!clear)
                throw new ComplianceHoldException("A current clear screening result is required before payout.");
        }
    }

    public async Task<PagedResult<ScreeningRecordDto>> GetScreeningsAsync(
        ScreeningSubjectType? subjectType,
        ScreeningStatus? status,
        bool? isBlocking,
        Guid? transferId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ScreeningRecords
            .AsNoTracking()
            .Include(x => x.Matches)
            .Where(x => !x.IsDeleted);
        if (subjectType.HasValue) query = query.Where(x => x.SubjectType == subjectType.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (isBlocking.HasValue) query = query.Where(x => x.IsBlocking == isBlocking.Value);
        if (transferId.HasValue) query = query.Where(x => x.TransferId == transferId.Value);

        var paged = await query
            .OrderByDescending(x => x.ScreenedAt)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<ScreeningRecordDto>
        {
            Items = paged.Items.Select(ToDto).ToList(),
            Meta = paged.Meta
        };
    }

    public Task<int> RunDueRescreeningAsync(
        int batchSize,
        CancellationToken ct = default) =>
        RunDueRescreeningCoreAsync(
            batchSize,
            initiatedByUserId: null,
            auditReason: null,
            auditOperatorAction: false,
            ct);

    public Task<int> RunDueRescreeningAsync(
        int batchSize,
        Guid initiatedByUserId,
        string reason,
        CancellationToken ct = default)
    {
        var cleanedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(cleanedReason))
            throw new InvalidOperationException(
                "A reason is required for a due-rescreening run.");

        if (cleanedReason.Length > 1000)
            throw new InvalidOperationException(
                "Reason cannot exceed 1000 characters.");

        return RunDueRescreeningCoreAsync(
            batchSize,
            initiatedByUserId,
            cleanedReason,
            auditOperatorAction: true,
            ct);
    }

    private async Task<int> RunDueRescreeningCoreAsync(
        int batchSize,
        Guid? initiatedByUserId,
        string? auditReason,
        bool auditOperatorAction,
        CancellationToken ct)
    {
        batchSize = Math.Clamp(batchSize, 1, 500);

        if (!_options.IsEnabled)
        {
            if (auditOperatorAction)
            {
                _audit.Stage(new AuditRecordRequest(
                    "DUE_COMPLIANCE_RESCREENING_RUN",
                    "Compliance",
                    nameof(ScreeningRecord),
                    "DUE",
                    null,
                    new
                    {
                        BatchSize = batchSize,
                        Processed = 0,
                        Reason = auditReason,
                        ScreeningEnabled = false
                    },
                    null,
                    initiatedByUserId));

                await _db.SaveChangesAsync(ct);
            }

            return 0;
        }

        var cutoff = DateTime.UtcNow;
        var processed = 0;

        var customerIds = await DueCustomerIds(cutoff)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var id in customerIds)
        {
            await ScreenCustomerAsync(
                id,
                ScreeningReason.OngoingRescreening,
                initiatedByUserId,
                null,
                ct);

            processed++;
        }

        if (processed < batchSize)
        {
            var businessIds = await DueBusinessIds(cutoff)
                .Take(batchSize - processed)
                .ToListAsync(ct);

            foreach (var id in businessIds)
            {
                await ScreenBusinessAsync(
                    id,
                    ScreeningReason.OngoingRescreening,
                    initiatedByUserId,
                    null,
                    ct);

                processed++;
            }
        }

        if (processed < batchSize)
        {
            var recipientIds = await DueRecipientIds(cutoff)
                .Take(batchSize - processed)
                .ToListAsync(ct);

            foreach (var id in recipientIds)
            {
                await ScreenRecipientAsync(
                    id,
                    ScreeningReason.OngoingRescreening,
                    initiatedByUserId,
                    null,
                    ct);

                processed++;
            }
        }

        if (processed < batchSize)
        {
            var beneficiaryIds = await DueBusinessBeneficiaryIds(cutoff)
                .Take(batchSize - processed)
                .ToListAsync(ct);

            foreach (var id in beneficiaryIds)
            {
                await ScreenBusinessBeneficiaryAsync(
                    id,
                    ScreeningReason.OngoingRescreening,
                    initiatedByUserId,
                    null,
                    ct);

                processed++;
            }
        }

        if (processed < batchSize)
        {
            var ownerIds = await DueBusinessBeneficialOwnerIds(cutoff)
                .Take(batchSize - processed)
                .ToListAsync(ct);

            foreach (var id in ownerIds)
            {
                await ScreenBusinessBeneficialOwnerAsync(
                    id,
                    ScreeningReason.OngoingRescreening,
                    initiatedByUserId,
                    null,
                    ct);

                processed++;
            }
        }

        if (auditOperatorAction)
        {
            _audit.Stage(new AuditRecordRequest(
                "DUE_COMPLIANCE_RESCREENING_RUN",
                "Compliance",
                nameof(ScreeningRecord),
                "DUE",
                null,
                new
                {
                    BatchSize = batchSize,
                    Processed = processed,
                    Reason = auditReason,
                    ScreeningEnabled = true,
                    Cutoff = cutoff
                },
                null,
                initiatedByUserId));
        }

        if (processed > 0 || auditOperatorAction)
            await _db.SaveChangesAsync(ct);

        return processed;
    }
    private async Task<ScreeningRecordDto?> ScreenSubjectAsync(
        ScreeningSubjectSnapshot subject,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId,
        CancellationToken ct)
    {
        if (!_options.IsEnabled)
            return null;

        var record = new ScreeningRecord
        {
            SubjectType = subject.SubjectType,
            Reason = reason,
            CustomerProfileId = subject.CustomerProfileId,
            BusinessProfileId = subject.BusinessProfileId,
            RecipientId = subject.RecipientId,
            BusinessBeneficiaryId = subject.BusinessBeneficiaryId,
            BusinessBeneficialOwnerId = subject.BusinessBeneficialOwnerId,
            TransferId = transferId,
            SubjectName = subject.Name,
            CountryCode = subject.CountryCode,
            DateOfBirth = subject.DateOfBirth,
            RegistrationNumberLastFour = subject.RegistrationNumberLastFour,
            ProviderCode = _provider.ProviderCode,
            Status = ScreeningStatus.ManualReview,
            ScreenedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.ScreeningValidityDays),
            RequestJson = JsonSerializer.Serialize(new
            {
                subject.SubjectType,
                subject.SubjectId,
                subject.Name,
                subject.Aliases,
                subject.CountryCode,
                subject.DateOfBirth,
                subject.RegistrationNumberLastFour,
                subject.IsDeclaredPep,
                reason
            }),
            CreatedByUserId = initiatedByUserId
        };
        _db.ScreeningRecords.Add(record);

        try
        {
            var result = await _provider.ScreenAsync(
                new ScreeningProviderRequest(
                    subject.SubjectType,
                    subject.SubjectId,
                    subject.Name,
                    subject.Aliases,
                    subject.CountryCode,
                    subject.DateOfBirth,
                    subject.RegistrationNumberLastFour,
                    reason),
                ct);

            record.ProviderCode = result.ProviderCode;
            record.ProviderReference = result.ProviderReference;
            record.Status = result.Status;
            record.HighestMatchScore = result.HighestMatchScore;
            record.IsBlocking = result.IsBlocking;
            record.ScreenedAt = result.ScreenedAt;
            record.ExpiresAt = result.ScreenedAt.AddDays(_options.ScreeningValidityDays);
            record.ResultJson = result.RawResultJson;

            foreach (var match in result.Matches)
            {
                record.Matches.Add(new ScreeningMatch
                {
                    WatchlistType = match.WatchlistType,
                    ListName = match.ListName,
                    MatchedName = match.MatchedName,
                    ProviderMatchId = match.ProviderMatchId,
                    MatchScore = match.MatchScore,
                    MatchReason = match.MatchReason,
                    CountryCode = match.CountryCode,
                    DateOfBirth = match.DateOfBirth,
                    RawJson = match.RawJson
                });
            }

            if (subject.IsDeclaredPep && record.Matches.All(x => x.WatchlistType != WatchlistType.Pep))
            {
                record.Matches.Add(new ScreeningMatch
                {
                    WatchlistType = WatchlistType.Pep,
                    ListName = "Customer declaration",
                    MatchedName = subject.Name,
                    ProviderMatchId = $"DECLARED-PEP-{subject.SubjectId:N}",
                    MatchScore = 100m,
                    MatchReason = "The beneficial owner was declared as a politically exposed person during KYB.",
                    CountryCode = subject.CountryCode,
                    DateOfBirth = subject.DateOfBirth,
                    RawJson = JsonSerializer.Serialize(new { subject.SubjectId, subject.IsDeclaredPep })
                });
                record.HighestMatchScore = Math.Max(record.HighestMatchScore, 100m);
                record.IsBlocking = record.IsBlocking || _options.BlockDeclaredPep;
                if (record.Status == ScreeningStatus.Clear)
                {
                    record.Status = _options.BlockDeclaredPep
                        ? ScreeningStatus.ConfirmedMatch
                        : ScreeningStatus.PotentialMatch;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            record.Status = ScreeningStatus.Failed;
            record.ErrorMessage = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
            record.IsBlocking = _options.FailClosedOnProviderError;
            record.ExpiresAt = DateTime.UtcNow.AddHours(1);
        }

        _db.ComplianceChecks.Add(new ComplianceCheck
        {
            CustomerProfileId = subject.CustomerProfileId,
            TransferId = transferId,
            CheckType = "WATCHLIST_SCREENING",
            Status = record.Status.ToString().ToUpperInvariant(),
            ProviderCode = record.ProviderCode,
            ProviderReference = record.ProviderReference,
            ResultJson = JsonSerializer.Serialize(new
            {
                record.SubjectType,
                record.SubjectName,
                record.Status,
                record.HighestMatchScore,
                record.IsBlocking,
                matchCount = record.Matches.Count,
                record.ErrorMessage
            }),
            CheckedAt = record.ScreenedAt
        });

        if (record.Status is ScreeningStatus.PotentialMatch or ScreeningStatus.ConfirmedMatch or ScreeningStatus.Failed)
            await _caseService.OpenScreeningCaseAsync(record, initiatedByUserId, ct);

        return ToDto(record);
    }

    private async Task EnsureTransferSubjectLinkAsync(
        ScreeningSubjectType subjectType,
        Guid subjectId,
        Guid transferId,
        CancellationToken ct)
    {
        var transfer = await _db.Transfers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transferId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Transfer not found for manual screening.");

        var linked = subjectType switch
        {
            ScreeningSubjectType.Customer => transfer.CustomerProfileId == subjectId,
            ScreeningSubjectType.Business => transfer.BusinessProfileId == subjectId,
            ScreeningSubjectType.Recipient => transfer.RecipientId == subjectId,
            ScreeningSubjectType.BusinessBeneficiary => transfer.BusinessBeneficiaryId == subjectId,
            ScreeningSubjectType.BusinessBeneficialOwner => await _db.BusinessBeneficialOwners.AsNoTracking()
                .AnyAsync(x =>
                    x.Id == subjectId &&
                    !x.IsDeleted &&
                    x.BusinessKybApplication.BusinessProfileId == transfer.BusinessProfileId,
                    ct),
            _ => false
        };
        if (!linked)
            throw new InvalidOperationException("The selected screening subject is not linked to the transfer.");
    }

    private IQueryable<Guid> DueCustomerIds(DateTime cutoff) =>
        _db.CustomerProfiles.AsNoTracking()
            .Where(x => !x.IsDeleted && !_db.ScreeningRecords.Any(s =>
                !s.IsDeleted &&
                s.CustomerProfileId == x.Id &&
                s.SubjectType == ScreeningSubjectType.Customer &&
                s.ExpiresAt.HasValue && s.ExpiresAt.Value > cutoff))
            .Select(x => x.Id);

    private IQueryable<Guid> DueBusinessIds(DateTime cutoff) =>
        _db.BusinessProfiles.AsNoTracking()
            .Where(x => !x.IsDeleted && !_db.ScreeningRecords.Any(s =>
                !s.IsDeleted &&
                s.BusinessProfileId == x.Id &&
                s.SubjectType == ScreeningSubjectType.Business &&
                s.ExpiresAt.HasValue && s.ExpiresAt.Value > cutoff))
            .Select(x => x.Id);

    private IQueryable<Guid> DueRecipientIds(DateTime cutoff) =>
        _db.Recipients.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && !_db.ScreeningRecords.Any(s =>
                !s.IsDeleted &&
                s.RecipientId == x.Id &&
                s.SubjectType == ScreeningSubjectType.Recipient &&
                s.ExpiresAt.HasValue && s.ExpiresAt.Value > cutoff))
            .Select(x => x.Id);

    private IQueryable<Guid> DueBusinessBeneficiaryIds(DateTime cutoff) =>
        _db.BusinessBeneficiaries.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && !_db.ScreeningRecords.Any(s =>
                !s.IsDeleted &&
                s.BusinessBeneficiaryId == x.Id &&
                s.SubjectType == ScreeningSubjectType.BusinessBeneficiary &&
                s.ExpiresAt.HasValue && s.ExpiresAt.Value > cutoff))
            .Select(x => x.Id);

    private IQueryable<Guid> DueBusinessBeneficialOwnerIds(DateTime cutoff) =>
        _db.BusinessBeneficialOwners.AsNoTracking()
            .Where(x => !x.IsDeleted && !_db.ScreeningRecords.Any(s =>
                !s.IsDeleted &&
                s.BusinessBeneficialOwnerId == x.Id &&
                s.SubjectType == ScreeningSubjectType.BusinessBeneficialOwner &&
                s.ExpiresAt.HasValue && s.ExpiresAt.Value > cutoff))
            .Select(x => x.Id);

    public static ScreeningRecordDto ToDto(ScreeningRecord x) => new(
        x.Id,
        x.SubjectType,
        x.Reason,
        x.Status,
        x.CustomerProfileId,
        x.BusinessProfileId,
        x.RecipientId,
        x.BusinessBeneficiaryId,
        x.BusinessBeneficialOwnerId,
        x.TransferId,
        x.SubjectName,
        x.CountryCode,
        x.ProviderCode,
        x.ProviderReference,
        x.HighestMatchScore,
        x.IsBlocking,
        x.ScreenedAt,
        x.ExpiresAt,
        x.ErrorMessage,
        x.Matches.OrderByDescending(m => m.MatchScore)
            .Select(m => new ScreeningMatchDto(
                m.Id,
                m.WatchlistType,
                m.ListName,
                m.MatchedName,
                m.ProviderMatchId,
                m.MatchScore,
                m.MatchReason,
                m.CountryCode,
                m.DateOfBirth,
                m.IsFalsePositive))
            .ToList());


    private static string BuildName(string firstName, string? middleName, string lastName) =>
        string.Join(" ", new[] { firstName, middleName, lastName }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));

    private static string? LastFour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= 4 ? cleaned : cleaned[^4..];
    }

    private sealed record ScreeningSubjectSnapshot(
        ScreeningSubjectType SubjectType,
        Guid SubjectId,
        string Name,
        IReadOnlyList<string> Aliases,
        string? CountryCode,
        DateTime? DateOfBirth,
        string? RegistrationNumberLastFour,
        Guid? CustomerProfileId,
        Guid? BusinessProfileId,
        Guid? RecipientId,
        Guid? BusinessBeneficiaryId,
        Guid? BusinessBeneficialOwnerId = null,
        bool IsDeclaredPep = false);
}
