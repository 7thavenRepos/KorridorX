using System.Globalization;
using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.BusinessTransfers;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Transfers;
using KorridorX.Services.References;
using KorridorX.Services.BusinessFunding;
using KorridorX.Services.Notifications;
using KorridorX.Services.Transfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessTransfers;

public class BusinessPaymentBatchService : IBusinessPaymentBatchService
{
    private static readonly string[] RequiredHeaders =
    [
        "businessbeneficiaryid",
        "destinationcurrencycode",
        "sourceamount",
        "purpose"
    ];

    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _accessService;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ITransferStatusService _transferStatusService;
    private readonly IBusinessFundingService _fundingService;
    private readonly INotificationQueueService _notifications;
    private readonly IComplianceLimitService _complianceLimitService;
    private readonly ITransferRiskService _transferRiskService;
    private readonly IComplianceScreeningService _screeningService;
    private readonly ITransactionMonitoringService _transactionMonitoringService;

    public BusinessPaymentBatchService(
        AppDbContext db,
        IBusinessAccessService accessService,
        IReferenceGenerator referenceGenerator,
        ITransferStatusService transferStatusService,
        IBusinessFundingService fundingService,
        INotificationQueueService notifications,
        IComplianceLimitService complianceLimitService,
        ITransferRiskService transferRiskService,
        IComplianceScreeningService screeningService,
        ITransactionMonitoringService transactionMonitoringService)
    {
        _db = db;
        _accessService = accessService;
        _referenceGenerator = referenceGenerator;
        _transferStatusService = transferStatusService;
        _fundingService = fundingService;
        _notifications = notifications;
        _complianceLimitService = complianceLimitService;
        _transferRiskService = transferRiskService;
        _screeningService = screeningService;
        _transactionMonitoringService = transactionMonitoringService;
    }

    public async Task<BusinessPaymentBatchDetailsDto> ImportAsync(
        Guid userId,
        string fileName,
        Stream csvStream,
        ImportBusinessPaymentBatchRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBatches, ct);
        var name = Clean(request.Name) ?? throw new InvalidOperationException("Batch name is required.");
        if (name.Length > 200) throw new InvalidOperationException("Batch name cannot exceed 200 characters.");
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Batch uploads must be CSV files.");

        if (!Enum.IsDefined(typeof(BusinessFundingSource), request.FundingSource))
            throw new InvalidOperationException("A valid business funding source is required.");

        var sourceCountry = NormalizeCode(request.SourceCountryCode);
        var sourceCurrency = NormalizeCode(request.SourceCurrencyCode);
        var business = await _db.BusinessProfiles.AsNoTracking()
            .FirstAsync(x => x.Id == access.BusinessProfileId && !x.IsDeleted, ct);
        if (!string.Equals(business.CountryCode, sourceCountry, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(business.OperatingCountryCode, sourceCountry, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The batch source country must match the business registered or operating country.");

        await EnsureCorridorAsync(sourceCountry, sourceCurrency, true, ct);

        var rows = await ParseCsvAsync(csvStream, ct);
        if (rows.Count == 0) throw new InvalidOperationException("The CSV file does not contain payment rows.");
        if (rows.Count > 1000) throw new InvalidOperationException("A payment batch cannot contain more than 1,000 rows.");

        var batch = new BusinessPaymentBatch
        {
            BusinessProfileId = access.BusinessProfileId,
            Reference = await GenerateUniqueBatchReferenceAsync(ct),
            Name = name,
            OriginalFileName = Truncate(Path.GetFileName(fileName), 255),
            SourceCountryCode = sourceCountry,
            SourceCurrencyCode = sourceCurrency,
            FundingSource = request.FundingSource,
            Status = BusinessPaymentBatchStatus.Draft,
            RequiredApprovals = access.RequiresTransferApproval ? access.RequiredTransferApprovals : 0,
            CreatedByUserId = userId
        };
        _db.BusinessPaymentBatches.Add(batch);

        for (var index = 0; index < rows.Count; index++)
        {
            var item = await BuildItemAsync(
                batch,
                index + 2,
                rows[index],
                userId,
                ct);
            batch.Items.Add(item);
        }

        RecalculateBatch(batch);
        batch.ValidatedAt = DateTime.UtcNow;
        batch.Status = batch.InvalidItems == 0 && batch.ValidItems > 0
            ? BusinessPaymentBatchStatus.ReadyForApproval
            : BusinessPaymentBatchStatus.ValidationFailed;

        await _db.SaveChangesAsync(ct);
        return await GetBatchAsync(userId, batch.Id, ct);
    }

    public async Task<PagedResult<BusinessPaymentBatchDto>> GetBatchesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewBatches, ct);
        var paged = await _db.BusinessPaymentBatches
            .AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<BusinessPaymentBatchDto>
        {
            Items = paged.Items.Select(ToDto).ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<BusinessPaymentBatchDetailsDto> GetBatchAsync(
        Guid userId,
        Guid batchId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewBatches, ct);
        var batch = await BatchQuery(access.BusinessProfileId)
            .FirstOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new InvalidOperationException("Business payment batch not found.");

        await RefreshExecutionStatusAsync(batch, ct);
        return ToDetailsDto(batch);
    }

    public async Task<BusinessPaymentBatchDetailsDto> SubmitAsync(
        Guid userId,
        Guid batchId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBatches, ct);
        var batch = await BatchQuery(access.BusinessProfileId)
            .FirstOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new InvalidOperationException("Business payment batch not found.");

        if (batch.Status != BusinessPaymentBatchStatus.ReadyForApproval)
            throw new InvalidOperationException("Only a fully validated batch can be submitted.");
        if (batch.InvalidItems > 0 || batch.ValidItems == 0)
            throw new InvalidOperationException("Resolve all invalid batch items before submission.");

        var requiresApproval = access.RequiresTransferApproval &&
            (!access.TransferApprovalThreshold.HasValue || batch.TotalPayableAmount >= access.TransferApprovalThreshold.Value);

        batch.SubmittedAt = DateTime.UtcNow;
        batch.LastUpdatedAt = DateTime.UtcNow;
        batch.LastUpdatedByUserId = userId;

        if (requiresApproval)
        {
            await EnsureApprovalCapacityAsync(
                access,
                batch.CreatedByUserId ?? userId,
                BusinessPermission.ApproveBatches,
                ct);

            batch.Status = BusinessPaymentBatchStatus.PendingApproval;
            batch.RequiredApprovals = access.RequiredTransferApprovals;
            foreach (var item in batch.Items.Where(x => x.Status == BusinessPaymentBatchItemStatus.Valid))
                item.Status = BusinessPaymentBatchItemStatus.PendingApproval;
        }
        else
        {
            batch.Status = BusinessPaymentBatchStatus.Approved;
            batch.RequiredApprovals = 0;
            batch.ApprovedAt = DateTime.UtcNow;

            if (batch.FundingSource == BusinessFundingSource.BusinessWallet)
            {
                await _fundingService.EnsureAvailableBalanceAsync(
                    batch.BusinessProfileId,
                    batch.SourceCurrencyCode,
                    batch.Items.Where(x => x.Status == BusinessPaymentBatchItemStatus.Valid)
                        .Sum(x => x.TotalPayableAmount),
                    ct);
            }

            await MaterializeTransfersAsync(batch, userId, ct);
        }

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(batch);
    }

    public async Task<BusinessPaymentBatchDetailsDto> ApproveAsync(
        Guid userId,
        Guid batchId,
        BusinessPaymentBatchDecisionRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ApproveBatches, ct);
        var batch = await BatchQuery(access.BusinessProfileId)
            .FirstOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new InvalidOperationException("Business payment batch not found.");

        if (batch.Status != BusinessPaymentBatchStatus.PendingApproval)
            throw new InvalidOperationException("Only a batch pending approval can be approved.");
        if (!access.AllowTransferCreatorApproval && batch.CreatedByUserId == userId)
            throw new InvalidOperationException("The batch creator cannot approve this batch.");

        var alreadyActioned = batch.Approvals.Any(x => x.ActionedByUserId == userId);
        if (alreadyActioned)
            throw new InvalidOperationException("This user has already actioned the batch approval.");

        batch.Approvals.Add(new BusinessApproval
        {
            BusinessProfileId = batch.BusinessProfileId,
            BusinessPaymentBatchId = batch.Id,
            ActionedByUserId = userId,
            Action = BusinessApprovalAction.Approved,
            Comment = Clean(request.Comment, 1000)
        });
        batch.ApprovalCount++;
        batch.LastUpdatedAt = DateTime.UtcNow;
        batch.LastUpdatedByUserId = userId;

        if (batch.ApprovalCount >= batch.RequiredApprovals)
        {
            batch.Status = BusinessPaymentBatchStatus.Approved;
            batch.ApprovedAt = DateTime.UtcNow;

            if (batch.FundingSource == BusinessFundingSource.BusinessWallet)
            {
                await _fundingService.EnsureAvailableBalanceAsync(
                    batch.BusinessProfileId,
                    batch.SourceCurrencyCode,
                    batch.Items.Where(x => x.Status == BusinessPaymentBatchItemStatus.PendingApproval)
                        .Sum(x => x.TotalPayableAmount),
                    ct);
            }

            await MaterializeTransfersAsync(batch, userId, ct);

            await _notifications.QueueBusinessAsync(
                batch.BusinessProfileId,
                "Business payment batch approved",
                $"Batch {batch.Reference} was approved. {batch.Items.Count(x => x.TransferId.HasValue)} transfer(s) were created.",
                "BusinessPaymentBatch",
                batch.Id,
                ct);
        }

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(batch);
    }

    public async Task<BusinessPaymentBatchDetailsDto> RejectAsync(
        Guid userId,
        Guid batchId,
        BusinessPaymentBatchDecisionRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ApproveBatches, ct);
        var batch = await BatchQuery(access.BusinessProfileId)
            .FirstOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new InvalidOperationException("Business payment batch not found.");

        if (batch.Status != BusinessPaymentBatchStatus.PendingApproval)
            throw new InvalidOperationException("Only a batch pending approval can be rejected.");
        if (!access.AllowTransferCreatorApproval && batch.CreatedByUserId == userId)
            throw new InvalidOperationException("The batch creator cannot reject this batch.");

        if (batch.Approvals.Any(x => x.ActionedByUserId == userId))
            throw new InvalidOperationException("This user has already actioned the batch approval.");

        var comment = Clean(request.Comment, 1000) ?? "Rejected by an authorized business approver.";
        batch.Approvals.Add(new BusinessApproval
        {
            BusinessProfileId = batch.BusinessProfileId,
            BusinessPaymentBatchId = batch.Id,
            ActionedByUserId = userId,
            Action = BusinessApprovalAction.Rejected,
            Comment = comment
        });
        batch.Status = BusinessPaymentBatchStatus.Rejected;
        batch.RejectedAt = DateTime.UtcNow;
        batch.RejectionReason = comment;
        batch.LastUpdatedAt = DateTime.UtcNow;
        batch.LastUpdatedByUserId = userId;
        foreach (var item in batch.Items.Where(x => x.TransferId == null))
            item.Status = BusinessPaymentBatchItemStatus.Rejected;

        await _notifications.QueueBusinessAsync(
            batch.BusinessProfileId,
            "Business payment batch rejected",
            $"Batch {batch.Reference} was rejected. {comment}",
            "BusinessPaymentBatch",
            batch.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(batch);
    }

    private async Task MaterializeTransfersAsync(
        BusinessPaymentBatch batch,
        Guid actionedByUserId,
        CancellationToken ct)
    {
        foreach (var item in batch.Items.Where(x =>
                     (x.Status is BusinessPaymentBatchItemStatus.Valid or BusinessPaymentBatchItemStatus.PendingApproval) &&
                     x.TransferId == null))
        {
            var trackedState = CaptureTrackedState();
            var originalQuote = item.TransferQuote;

            try
            {
                await EnsureDestinationStillValidAsync(item, ct);
                await RefreshQuoteIfNeededAsync(batch, item, actionedByUserId, ct);
                var quote = item.TransferQuote
                    ?? await _db.TransferQuotes.FirstAsync(x => x.Id == item.TransferQuoteId, ct);

                var transfer = new Transfer
                {
                    Reference = await GenerateUniqueTransferReferenceAsync(ct),
                    BusinessProfileId = batch.BusinessProfileId,
                    BusinessBeneficiaryId = item.BusinessBeneficiaryId,
                    BusinessBeneficiaryBankAccountId = item.BusinessBeneficiaryBankAccountId,
                    BusinessBeneficiaryMobileWalletId = item.BusinessBeneficiaryMobileWalletId,
                    TransferQuoteId = quote.Id,
                    TransferType = quote.TransferType,
                    Purpose = item.Purpose,
                    PurposeNote = item.PurposeNote,
                    SourceCountryCode = quote.SourceCountryCode,
                    DestinationCountryCode = quote.DestinationCountryCode,
                    SourceCurrencyCode = quote.SourceCurrencyCode,
                    DestinationCurrencyCode = quote.DestinationCurrencyCode,
                    SourceAmount = quote.SourceAmount,
                    DestinationAmount = quote.DestinationAmount,
                    FeeAmount = quote.FeeAmount,
                    FeeCurrencyCode = quote.FeeCurrencyCode,
                    TotalPayableAmount = quote.TotalPayableAmount,
                    CustomerRate = quote.CustomerRate,
                    ProviderRate = quote.ProviderRate,
                    ProviderCode = quote.ProviderCode,
                    BusinessFundingSource = batch.FundingSource,
                    ApprovalStatus = BusinessApprovalStatus.Approved,
                    RequiredApprovals = batch.RequiredApprovals,
                    ApprovalCount = batch.ApprovalCount,
                    RequestedByUserId = batch.CreatedByUserId,
                    SubmittedForApprovalAt = batch.SubmittedAt,
                    ApprovedAt = batch.ApprovedAt ?? DateTime.UtcNow,
                    FinalApprovedByUserId = actionedByUserId,
                    CreatedByUserId = batch.CreatedByUserId,
                    Status = TransferStatus.Draft
                };

                await _complianceLimitService.EnsureWithinLimitsAsync(transfer, ct);
                _db.Transfers.Add(transfer);

                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.PendingPayment,
                    new TransferStatusTransitionContext(
                        Source: "BusinessBatch",
                        Reason: $"Approved business batch {batch.Reference} is waiting for funding confirmation.",
                        ChangedByUserId: actionedByUserId,
                        EventType: "BUSINESS_BATCH_TRANSFER_CREATED",
                        Title: "Batch transfer created",
                        Description: $"Created from business payment batch {batch.Reference}."));

                await _transferRiskService.AssessAsync(transfer, actionedByUserId, ct);
                await _screeningService.ScreenTransferAsync(transfer, actionedByUserId, ct);
                await _transactionMonitoringService.MonitorAsync(transfer, actionedByUserId, ct);

                if (batch.FundingSource == BusinessFundingSource.BusinessWallet)
                {
                    await _fundingService.ReserveTransferAsync(
                        transfer,
                        actionedByUserId,
                        "BusinessBatch",
                        batch.Id,
                        ct);
                }

                quote.IsUsed = true;
                quote.UsedAt = DateTime.UtcNow;
                quote.LastUpdatedAt = DateTime.UtcNow;
                item.Transfer = transfer;
                item.TransferId = transfer.Id;
                item.Status = BusinessPaymentBatchItemStatus.TransferCreated;
                item.ValidationErrors = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RestoreTrackedState(trackedState);
                item.Transfer = null;
                item.TransferId = null;
                item.TransferQuote = originalQuote;
                item.Status = BusinessPaymentBatchItemStatus.Failed;
                item.ValidationErrors = Truncate(ex.Message, 4000);
            }
        }

        var materializedCount = batch.Items.Count(x => x.TransferId.HasValue);
        batch.FailedItems = batch.Items.Count(x => x.Status == BusinessPaymentBatchItemStatus.Failed);
        batch.ProcessingStartedAt ??= DateTime.UtcNow;
        batch.LastUpdatedAt = DateTime.UtcNow;

        if (materializedCount == 0 && batch.FailedItems >= batch.ValidItems)
        {
            batch.Status = BusinessPaymentBatchStatus.Failed;
            batch.CompletedAt = DateTime.UtcNow;
            batch.FailureReason = "No validated batch item could be converted into a transfer.";
        }
        else if (batch.FailedItems > 0)
        {
            batch.Status = BusinessPaymentBatchStatus.PartiallyCompleted;
            batch.FailureReason = $"{batch.FailedItems} batch item(s) could not be converted into transfers.";
        }
        else
        {
            batch.Status = BusinessPaymentBatchStatus.Processing;
            batch.FailureReason = null;
        }
    }

    private IReadOnlyList<TrackedEntrySnapshot> CaptureTrackedState() =>
        _db.ChangeTracker.Entries()
            .Select(entry => new TrackedEntrySnapshot(
                entry.Entity,
                entry.State,
                entry.CurrentValues.Clone()))
            .ToList();

    private void RestoreTrackedState(IReadOnlyList<TrackedEntrySnapshot> snapshot)
    {
        var originalEntities = snapshot
            .Select(x => x.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);

        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (!originalEntities.Contains(entry.Entity))
                entry.State = EntityState.Detached;
        }

        foreach (var saved in snapshot)
        {
            var entry = _db.Entry(saved.Entity);
            entry.CurrentValues.SetValues(saved.Values);
            entry.State = saved.State;
        }
    }

    private sealed record TrackedEntrySnapshot(
        object Entity,
        EntityState State,
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues Values);

    private async Task<BusinessPaymentBatchItem> BuildItemAsync(
        BusinessPaymentBatch batch,
        int rowNumber,
        IReadOnlyDictionary<string, string> row,
        Guid userId,
        CancellationToken ct)
    {
        var errors = new List<string>();
        var externalReference = Clean(Read(row, "externalreference"));
        if (externalReference?.Length > 100)
        {
            errors.Add("externalReference cannot exceed 100 characters.");
            externalReference = Truncate(externalReference, 100);
        }

        var purposeNote = Clean(Read(row, "purposenote"));
        if (purposeNote?.Length > 500)
        {
            errors.Add("purposeNote cannot exceed 500 characters.");
            purposeNote = Truncate(purposeNote, 500);
        }

        var item = new BusinessPaymentBatchItem
        {
            BusinessPaymentBatch = batch,
            RowNumber = rowNumber,
            ExternalReference = externalReference,
            PurposeNote = purposeNote,
            CreatedByUserId = userId
        };

        if (!Guid.TryParse(Read(row, "businessbeneficiaryid"), out var beneficiaryId))
            errors.Add("businessBeneficiaryId must be a valid GUID.");
        else
            item.BusinessBeneficiaryId = beneficiaryId;

        var bankIdText = Read(row, "bankaccountid");
        var walletIdText = Read(row, "mobilewalletid");
        if (!string.IsNullOrWhiteSpace(bankIdText) && Guid.TryParse(bankIdText, out var bankId))
            item.BusinessBeneficiaryBankAccountId = bankId;
        else if (!string.IsNullOrWhiteSpace(bankIdText))
            errors.Add("bankAccountId must be a valid GUID.");
        if (!string.IsNullOrWhiteSpace(walletIdText) && Guid.TryParse(walletIdText, out var walletId))
            item.BusinessBeneficiaryMobileWalletId = walletId;
        else if (!string.IsNullOrWhiteSpace(walletIdText))
            errors.Add("mobileWalletId must be a valid GUID.");
        if (item.BusinessBeneficiaryBankAccountId.HasValue == item.BusinessBeneficiaryMobileWalletId.HasValue)
            errors.Add("Exactly one bankAccountId or mobileWalletId is required.");

        item.DestinationCurrencyCode = NormalizeCodeOrEmpty(Read(row, "destinationcurrencycode"));
        if (string.IsNullOrWhiteSpace(item.DestinationCurrencyCode))
        {
            errors.Add("destinationCurrencyCode is required.");
        }
        else if (item.DestinationCurrencyCode.Length > 10)
        {
            errors.Add("destinationCurrencyCode cannot exceed 10 characters.");
            item.DestinationCurrencyCode = Truncate(item.DestinationCurrencyCode, 10);
        }
        if (!decimal.TryParse(Read(row, "sourceamount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            errors.Add("sourceAmount must be a positive number.");
        else
            item.SourceAmount = amount;

        if (!TryParsePurpose(Read(row, "purpose"), out var purpose))
            errors.Add("purpose must be a valid TransferPurpose name or numeric value.");
        else
            item.Purpose = purpose;

        if (errors.Count == 0)
        {
            try
            {
                await ValidateAndQuoteAsync(batch, item, userId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        item.Status = errors.Count == 0
            ? BusinessPaymentBatchItemStatus.Valid
            : BusinessPaymentBatchItemStatus.Invalid;
        item.ValidationErrors = errors.Count == 0
            ? null
            : Truncate(string.Join(" | ", errors), 4000);
        return item;
    }

    private async Task ValidateAndQuoteAsync(
        BusinessPaymentBatch batch,
        BusinessPaymentBatchItem item,
        Guid userId,
        CancellationToken ct)
    {
        var beneficiary = await _db.BusinessBeneficiaries
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == item.BusinessBeneficiaryId &&
                x.BusinessProfileId == batch.BusinessProfileId &&
                x.IsActive &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business beneficiary not found or inactive.");

        item.DestinationCountryCode = beneficiary.CountryCode;
        var transferType = beneficiary.BeneficiaryType == BusinessBeneficiaryType.Business
            ? TransferType.BusinessToBusiness
            : TransferType.BusinessToConsumer;

        if (item.BusinessBeneficiaryBankAccountId.HasValue)
        {
            var account = await _db.BusinessBeneficiaryBankAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == item.BusinessBeneficiaryBankAccountId &&
                    x.BusinessBeneficiaryId == beneficiary.Id &&
                    x.IsActive &&
                    !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException("Beneficiary bank account not found or inactive.");
            if (!account.IsVerified) throw new InvalidOperationException("Beneficiary bank account is not verified.");
            if (!string.Equals(account.CountryCode, beneficiary.CountryCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bank-account country does not match the beneficiary country.");
            if (!string.Equals(account.CurrencyCode, item.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bank-account currency does not match destinationCurrencyCode.");
        }
        else
        {
            var wallet = await _db.BusinessBeneficiaryMobileWallets.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == item.BusinessBeneficiaryMobileWalletId &&
                    x.BusinessBeneficiaryId == beneficiary.Id &&
                    x.IsActive &&
                    !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException("Beneficiary mobile wallet not found or inactive.");
            if (!wallet.IsVerified) throw new InvalidOperationException("Beneficiary mobile wallet is not verified.");
            if (!string.Equals(wallet.CountryCode, beneficiary.CountryCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mobile-wallet country does not match the beneficiary country.");
            if (!string.Equals(wallet.CurrencyCode, item.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mobile-wallet currency does not match destinationCurrencyCode.");
        }

        await EnsureCorridorAsync(item.DestinationCountryCode, item.DestinationCurrencyCode, false, ct);
        var quote = await CreateQuoteAsync(batch, item, transferType, userId, ct);
        item.TransferQuote = quote;
        item.TransferQuoteId = quote.Id;
        CopyQuote(item, quote);
    }

    private async Task<TransferQuote> CreateQuoteAsync(
        BusinessPaymentBatch batch,
        BusinessPaymentBatchItem item,
        TransferType transferType,
        Guid userId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rate = await _db.ExchangeRates.AsNoTracking()
            .Where(x =>
                x.SourceCurrencyCode == batch.SourceCurrencyCode &&
                x.DestinationCurrencyCode == item.DestinationCurrencyCode &&
                x.IsActive &&
                x.EffectiveFrom <= now &&
                (x.EffectiveTo == null || x.EffectiveTo > now))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Exchange rate is not available for this batch item.");

        var fee = await _db.TransferFees.AsNoTracking()
            .Where(x =>
                x.SourceCountryCode == batch.SourceCountryCode &&
                x.DestinationCountryCode == item.DestinationCountryCode &&
                x.SourceCurrencyCode == batch.SourceCurrencyCode &&
                x.DestinationCurrencyCode == item.DestinationCurrencyCode &&
                x.TransferType == transferType &&
                x.IsActive &&
                item.SourceAmount >= x.MinAmount &&
                (x.MaxAmount == null || item.SourceAmount <= x.MaxAmount))
            .OrderByDescending(x => x.MinAmount)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Transfer fee is not configured for this batch item corridor.");

        var feeAmount = Math.Round(fee.FixedFee + item.SourceAmount * fee.PercentageFee / 100m, 2, MidpointRounding.AwayFromZero);
        var quote = new TransferQuote
        {
            BusinessProfileId = batch.BusinessProfileId,
            SourceCountryCode = batch.SourceCountryCode,
            DestinationCountryCode = item.DestinationCountryCode,
            SourceCurrencyCode = batch.SourceCurrencyCode,
            DestinationCurrencyCode = item.DestinationCurrencyCode,
            TransferType = transferType,
            SourceAmount = item.SourceAmount,
            DestinationAmount = Math.Round(item.SourceAmount * rate.CustomerRate, 2, MidpointRounding.AwayFromZero),
            ProviderRate = rate.ProviderRate,
            CustomerRate = rate.CustomerRate,
            FeeAmount = feeAmount,
            FeeCurrencyCode = fee.FeeCurrencyCode,
            TotalPayableAmount = item.SourceAmount + feeAmount,
            ProviderCode = rate.ProviderCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedByUserId = userId
        };
        _db.TransferQuotes.Add(quote);
        return quote;
    }

    private async Task EnsureDestinationStillValidAsync(
        BusinessPaymentBatchItem item,
        CancellationToken ct)
    {
        var beneficiary = await _db.BusinessBeneficiaries.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == item.BusinessBeneficiaryId &&
                x.IsActive &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("The batch beneficiary is no longer active.");

        if (!string.Equals(beneficiary.CountryCode, item.DestinationCountryCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The beneficiary country changed after batch validation.");

        if (item.BusinessBeneficiaryBankAccountId.HasValue)
        {
            var account = await _db.BusinessBeneficiaryBankAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == item.BusinessBeneficiaryBankAccountId.Value &&
                    x.BusinessBeneficiaryId == beneficiary.Id &&
                    x.IsActive &&
                    !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException("The batch bank account is no longer active.");

            if (!account.IsVerified)
                throw new InvalidOperationException("The batch bank account is no longer verified.");
            if (!string.Equals(account.CountryCode, item.DestinationCountryCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(account.CurrencyCode, item.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The batch bank account no longer matches the validated corridor.");
        }
        else if (item.BusinessBeneficiaryMobileWalletId.HasValue)
        {
            var wallet = await _db.BusinessBeneficiaryMobileWallets.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == item.BusinessBeneficiaryMobileWalletId.Value &&
                    x.BusinessBeneficiaryId == beneficiary.Id &&
                    x.IsActive &&
                    !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException("The batch mobile wallet is no longer active.");

            if (!wallet.IsVerified)
                throw new InvalidOperationException("The batch mobile wallet is no longer verified.");
            if (!string.Equals(wallet.CountryCode, item.DestinationCountryCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(wallet.CurrencyCode, item.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The batch mobile wallet no longer matches the validated corridor.");
        }
        else
        {
            throw new InvalidOperationException("The batch item has no payout destination.");
        }
    }

    private async Task RefreshQuoteIfNeededAsync(
        BusinessPaymentBatch batch,
        BusinessPaymentBatchItem item,
        Guid userId,
        CancellationToken ct)
    {
        var quote = item.TransferQuote ?? (item.TransferQuoteId.HasValue
            ? await _db.TransferQuotes.FirstOrDefaultAsync(x => x.Id == item.TransferQuoteId.Value, ct)
            : null);
        if (quote is not null && !quote.IsExpired && !quote.IsUsed)
        {
            item.TransferQuote = quote;
            return;
        }

        var beneficiary = item.BusinessBeneficiary ?? await _db.BusinessBeneficiaries
            .FirstAsync(x => x.Id == item.BusinessBeneficiaryId, ct);
        var type = beneficiary.BeneficiaryType == BusinessBeneficiaryType.Business
            ? TransferType.BusinessToBusiness
            : TransferType.BusinessToConsumer;
        var replacement = await CreateQuoteAsync(batch, item, type, userId, ct);
        item.TransferQuote = replacement;
        item.TransferQuoteId = replacement.Id;
        CopyQuote(item, replacement);
    }

    private async Task RefreshExecutionStatusAsync(BusinessPaymentBatch batch, CancellationToken ct)
    {
        if (batch.Status is not BusinessPaymentBatchStatus.Processing and not BusinessPaymentBatchStatus.PartiallyCompleted)
            return;

        if (!batch.Items.Any(x => x.TransferId.HasValue)) return;

        foreach (var item in batch.Items.Where(x => x.TransferId.HasValue))
        {
            var status = item.Transfer?.Status ?? await _db.Transfers.AsNoTracking()
                .Where(x => x.Id == item.TransferId)
                .Select(x => x.Status)
                .FirstAsync(ct);
            item.Status = status switch
            {
                TransferStatus.Completed => BusinessPaymentBatchItemStatus.Completed,
                TransferStatus.Failed or TransferStatus.Rejected or TransferStatus.Refunded => BusinessPaymentBatchItemStatus.Failed,
                _ => BusinessPaymentBatchItemStatus.Processing
            };
        }

        batch.CompletedItems = batch.Items.Count(x => x.Status == BusinessPaymentBatchItemStatus.Completed);
        batch.FailedItems = batch.Items.Count(x => x.Status == BusinessPaymentBatchItemStatus.Failed);
        var terminal = batch.CompletedItems + batch.FailedItems;
        if (terminal == batch.ValidItems)
        {
            batch.Status = batch.FailedItems == 0
                ? BusinessPaymentBatchStatus.Completed
                : batch.CompletedItems > 0
                    ? BusinessPaymentBatchStatus.PartiallyCompleted
                    : BusinessPaymentBatchStatus.Failed;
            batch.CompletedAt = DateTime.UtcNow;
        }
        else if (batch.CompletedItems > 0 || batch.FailedItems > 0)
        {
            batch.Status = BusinessPaymentBatchStatus.PartiallyCompleted;
        }
        batch.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<BusinessPaymentBatch> BatchQuery(Guid businessProfileId) =>
        _db.BusinessPaymentBatches
            .Include(x => x.Items).ThenInclude(x => x.BusinessBeneficiary)
            .Include(x => x.Items).ThenInclude(x => x.TransferQuote)
            .Include(x => x.Items).ThenInclude(x => x.Transfer)
            .Include(x => x.Approvals)
            .Where(x => x.BusinessProfileId == businessProfileId && !x.IsDeleted);

    private static void RecalculateBatch(BusinessPaymentBatch batch)
    {
        batch.TotalItems = batch.Items.Count;
        batch.ValidItems = batch.Items.Count(x => x.Status == BusinessPaymentBatchItemStatus.Valid);
        batch.InvalidItems = batch.Items.Count(x => x.Status == BusinessPaymentBatchItemStatus.Invalid);
        batch.TotalSourceAmount = batch.Items.Where(x => x.Status == BusinessPaymentBatchItemStatus.Valid).Sum(x => x.SourceAmount);
        batch.TotalFeeAmount = batch.Items.Where(x => x.Status == BusinessPaymentBatchItemStatus.Valid).Sum(x => x.FeeAmount);
        batch.TotalPayableAmount = batch.Items.Where(x => x.Status == BusinessPaymentBatchItemStatus.Valid).Sum(x => x.TotalPayableAmount);
    }

    private static void CopyQuote(BusinessPaymentBatchItem item, TransferQuote quote)
    {
        item.DestinationAmount = quote.DestinationAmount;
        item.FeeAmount = quote.FeeAmount;
        item.TotalPayableAmount = quote.TotalPayableAmount;
        item.CustomerRate = quote.CustomerRate;
        item.ProviderRate = quote.ProviderRate;
    }

    private static BusinessPaymentBatchDetailsDto ToDetailsDto(BusinessPaymentBatch batch) =>
        new(
            ToDto(batch),
            batch.Items.OrderBy(x => x.RowNumber).Select(x => new BusinessPaymentBatchItemDto(
                x.Id,
                x.RowNumber,
                x.ExternalReference,
                x.BusinessBeneficiaryId,
                x.BusinessBeneficiary?.Name,
                x.BusinessBeneficiaryBankAccountId,
                x.BusinessBeneficiaryMobileWalletId,
                x.DestinationCountryCode,
                x.DestinationCurrencyCode,
                x.SourceAmount,
                x.DestinationAmount,
                x.FeeAmount,
                x.TotalPayableAmount,
                x.Purpose,
                x.PurposeNote,
                x.Status,
                x.ValidationErrors,
                x.TransferQuoteId,
                x.TransferId)).ToList(),
            batch.Approvals.OrderBy(x => x.ActionedAt)
                .Select(x => new BusinessApprovalDto(x.Id, x.ActionedByUserId, x.Action, x.Comment, x.ActionedAt))
                .ToList());

    private static BusinessPaymentBatchDto ToDto(BusinessPaymentBatch x) =>
        new(
            x.Id,
            x.Reference,
            x.Name,
            x.OriginalFileName,
            x.SourceCountryCode,
            x.SourceCurrencyCode,
            x.FundingSource,
            x.Status,
            x.TotalItems,
            x.ValidItems,
            x.InvalidItems,
            x.CompletedItems,
            x.FailedItems,
            x.TotalSourceAmount,
            x.TotalFeeAmount,
            x.TotalPayableAmount,
            x.RequiredApprovals,
            x.ApprovalCount,
            x.ValidatedAt,
            x.SubmittedAt,
            x.ApprovedAt,
            x.RejectedAt,
            x.CompletedAt,
            x.RejectionReason,
            x.FailureReason,
            x.CreatedAt,
            x.LastUpdatedAt);

    private async Task EnsureApprovalCapacityAsync(
        BusinessAccessContext access,
        Guid creatorUserId,
        BusinessPermission approvalPermission,
        CancellationToken ct)
    {
        var approverIds = new HashSet<Guid>();

        if (access.AllowTransferCreatorApproval || access.OwnerUserId != creatorUserId)
            approverIds.Add(access.OwnerUserId);

        var members = await _db.BusinessUsers.AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => new { x.UserId, x.Role, x.Permissions })
            .ToListAsync(ct);

        foreach (var member in members)
        {
            if (!access.AllowTransferCreatorApproval && member.UserId == creatorUserId)
                continue;

            var permissions = member.Permissions == BusinessPermission.None
                ? BusinessAccessService.DefaultPermissions(member.Role)
                : member.Permissions;

            if ((permissions & approvalPermission) == approvalPermission)
                approverIds.Add(member.UserId);
        }

        if (approverIds.Count < access.RequiredTransferApprovals)
        {
            throw new InvalidOperationException(
                $"The business requires {access.RequiredTransferApprovals} approval(s), but only {approverIds.Count} eligible approver(s) are available.");
        }
    }

    private async Task EnsureCorridorAsync(string countryCode, string currencyCode, bool sending, CancellationToken ct)
    {
        var exists = await _db.CountryCurrencies.AsNoTracking().AnyAsync(x =>
            x.CountryCode == countryCode &&
            x.CurrencyCode == currencyCode &&
            x.Country.IsSupported &&
            x.Currency.IsSupported &&
            (sending ? x.CanSend && x.Country.IsSendCountry : x.CanReceive && x.Country.IsReceiveCountry),
            ct);
        if (!exists)
            throw new InvalidOperationException($"Currency '{currencyCode}' is not supported for {(sending ? "sending from" : "receiving in")} '{countryCode}'.");
    }

    private async Task<string> GenerateUniqueBatchReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateBusinessBatchReference();
            if (!await _db.BusinessPaymentBatches.AsNoTracking().AnyAsync(x => x.Reference == reference, ct)) return reference;
        }
        throw new InvalidOperationException("Unable to generate a unique batch reference.");
    }

    private async Task<string> GenerateUniqueTransferReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateTransferReference();
            if (!await _db.Transfers.AsNoTracking().AnyAsync(x => x.Reference == reference, ct)) return reference;
        }
        throw new InvalidOperationException("Unable to generate a unique transfer reference.");
    }

    private static async Task<List<IReadOnlyDictionary<string, string>>> ParseCsvAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(ct)
            ?? throw new InvalidOperationException("The CSV file is empty.");
        var headers = ParseCsvLine(headerLine).Select(NormalizeHeader).ToList();
        foreach (var required in RequiredHeaders)
            if (!headers.Contains(required))
                throw new InvalidOperationException($"The CSV file is missing the required '{required}' column.");

        var rows = new List<IReadOnlyDictionary<string, string>>();
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                row[headers[i]] = i < values.Count ? values[i].Trim() : "";
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }
        if (quoted) throw new InvalidOperationException("The CSV contains an unterminated quoted value.");
        result.Add(current.ToString());
        return result;
    }

    private static bool TryParsePurpose(string? value, out TransferPurpose purpose)
    {
        if (int.TryParse(value, out var numeric) && Enum.IsDefined(typeof(TransferPurpose), numeric))
        {
            purpose = (TransferPurpose)numeric;
            return true;
        }
        return Enum.TryParse(value, true, out purpose) && Enum.IsDefined(typeof(TransferPurpose), purpose);
    }

    private static string Read(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) ? value : "";
    private static string NormalizeHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Country and currency codes are required.");

        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 10)
            throw new InvalidOperationException("Country and currency codes cannot exceed 10 characters.");

        return code;
    }

    private static string NormalizeCodeOrEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToUpperInvariant();
    private static string? Clean(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (maxLength.HasValue && cleaned.Length > maxLength.Value)
            throw new InvalidOperationException($"Value cannot exceed {maxLength.Value} characters.");
        return cleaned;
    }
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
