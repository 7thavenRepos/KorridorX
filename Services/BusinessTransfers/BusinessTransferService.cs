using KorridorX.Data;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Dtos.Fx;
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

public class BusinessTransferService : IBusinessTransferService
{
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

    public BusinessTransferService(
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

    public async Task<TransferQuoteDto> CreateQuoteAsync(
        Guid userId,
        CreateBusinessTransferQuoteRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(
            userId,
            BusinessPermission.CreateTransfers,
            ct);

        if (request.TransferType is not TransferType.BusinessToConsumer and not TransferType.BusinessToBusiness)
        {
            throw new InvalidOperationException("Business quotes must be BusinessToConsumer or BusinessToBusiness.");
        }

        if (request.SourceAmount <= 0)
        {
            throw new InvalidOperationException("Source amount must be greater than zero.");
        }

        var sourceCountry = NormalizeCode(request.SourceCountryCode);
        var destinationCountry = NormalizeCode(request.DestinationCountryCode);
        var sourceCurrency = NormalizeCode(request.SourceCurrencyCode);
        var destinationCurrency = NormalizeCode(request.DestinationCurrencyCode);

        var business = await _db.BusinessProfiles
            .AsNoTracking()
            .FirstAsync(x => x.Id == access.BusinessProfileId && !x.IsDeleted, ct);

        if (!string.Equals(business.CountryCode, sourceCountry, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(business.OperatingCountryCode, sourceCountry, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The source country must match the business registered or operating country.");
        }

        await EnsureCorridorAsync(sourceCountry, sourceCurrency, true, ct);
        await EnsureCorridorAsync(destinationCountry, destinationCurrency, false, ct);

        var rate = await GetRateAsync(sourceCurrency, destinationCurrency, ct);
        var fee = await GetFeeAsync(
            sourceCountry,
            destinationCountry,
            sourceCurrency,
            destinationCurrency,
            request.TransferType,
            request.SourceAmount,
            ct);

        var feeAmount = Math.Round(
            fee.FixedFee + (request.SourceAmount * fee.PercentageFee / 100m),
            2,
            MidpointRounding.AwayFromZero);

        var quote = new TransferQuote
        {
            BusinessProfileId = access.BusinessProfileId,
            SourceCountryCode = sourceCountry,
            DestinationCountryCode = destinationCountry,
            SourceCurrencyCode = sourceCurrency,
            DestinationCurrencyCode = destinationCurrency,
            TransferType = request.TransferType,
            SourceAmount = request.SourceAmount,
            DestinationAmount = Math.Round(request.SourceAmount * rate.CustomerRate, 2, MidpointRounding.AwayFromZero),
            ProviderRate = rate.ProviderRate,
            CustomerRate = rate.CustomerRate,
            FeeAmount = feeAmount,
            FeeCurrencyCode = fee.FeeCurrencyCode,
            TotalPayableAmount = request.SourceAmount + feeAmount,
            ProviderCode = rate.ProviderCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedByUserId = userId
        };

        _db.TransferQuotes.Add(quote);
        await _db.SaveChangesAsync(ct);
        return ToQuoteDto(quote);
    }

    public async Task<TransferQuoteDto> GetQuoteAsync(
        Guid userId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewTransfers, ct);
        var quote = await _db.TransferQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == quoteId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business transfer quote not found.");

        return ToQuoteDto(quote);
    }

    public async Task<BusinessTransferDetailsDto> CreateTransferAsync(
        Guid userId,
        CreateBusinessTransferRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.CreateTransfers, ct);
        ValidateDestinationSelection(request.BusinessBeneficiaryBankAccountId, request.BusinessBeneficiaryMobileWalletId);

        if (!Enum.IsDefined(typeof(TransferPurpose), request.Purpose))
            throw new InvalidOperationException("A valid transfer purpose is required.");
        if (!Enum.IsDefined(typeof(BusinessFundingSource), request.FundingSource))
            throw new InvalidOperationException("A valid business funding source is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var quote = await _db.TransferQuotes
            .FirstOrDefaultAsync(x =>
                x.Id == request.TransferQuoteId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business transfer quote not found.");

        ValidateQuote(quote);

        var beneficiary = await _db.BusinessBeneficiaries
            .Include(x => x.BankAccounts)
            .Include(x => x.MobileWallets)
            .FirstOrDefaultAsync(x =>
                x.Id == request.BusinessBeneficiaryId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                x.IsActive &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business beneficiary not found.");

        ValidateBeneficiaryType(beneficiary.BeneficiaryType, quote.TransferType);
        var bankAccount = request.BusinessBeneficiaryBankAccountId.HasValue
            ? beneficiary.BankAccounts.FirstOrDefault(x =>
                x.Id == request.BusinessBeneficiaryBankAccountId.Value &&
                x.IsActive &&
                !x.IsDeleted)
            : null;
        var mobileWallet = request.BusinessBeneficiaryMobileWalletId.HasValue
            ? beneficiary.MobileWallets.FirstOrDefault(x =>
                x.Id == request.BusinessBeneficiaryMobileWalletId.Value &&
                x.IsActive &&
                !x.IsDeleted)
            : null;

        ValidateDestination(beneficiary.CountryCode, quote, bankAccount, mobileWallet);

        var approvalRequired = RequiresApproval(access, quote.TotalPayableAmount);
        if (approvalRequired)
        {
            await EnsureApprovalCapacityAsync(
                access,
                userId,
                BusinessPermission.ApproveTransfers,
                ct);
        }

        var transfer = new Transfer
        {
            Reference = await GenerateUniqueReferenceAsync(ct),
            BusinessProfileId = access.BusinessProfileId,
            BusinessBeneficiaryId = beneficiary.Id,
            BusinessBeneficiaryBankAccountId = bankAccount?.Id,
            BusinessBeneficiaryMobileWalletId = mobileWallet?.Id,
            TransferQuoteId = quote.Id,
            TransferType = quote.TransferType,
            Purpose = request.Purpose,
            PurposeNote = Clean(request.PurposeNote, 500),
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
            BusinessFundingSource = request.FundingSource,
            ApprovalStatus = approvalRequired ? BusinessApprovalStatus.Pending : BusinessApprovalStatus.NotRequired,
            RequiredApprovals = approvalRequired ? access.RequiredTransferApprovals : 0,
            RequestedByUserId = userId,
            SubmittedForApprovalAt = approvalRequired ? DateTime.UtcNow : null,
            CreatedByUserId = userId,
            Status = TransferStatus.Draft
        };

        await _complianceLimitService.EnsureWithinLimitsAsync(transfer, ct);

        _db.Transfers.Add(transfer);

        _transferStatusService.ApplyTransition(
            transfer,
            approvalRequired
                ? TransferStatus.PendingApproval
                : TransferStatus.PendingPayment,
            new TransferStatusTransitionContext(
                Source: "Business",
                Reason: approvalRequired
                    ? "Business transfer submitted for approval."
                    : "Business transfer is approved and waiting for funding confirmation.",
                ChangedByUserId: userId,
                EventType: approvalRequired ? "BUSINESS_TRANSFER_PENDING_APPROVAL" : "BUSINESS_TRANSFER_CREATED",
                Title: approvalRequired ? "Approval required" : "Business transfer created",
                Description: approvalRequired
                    ? "The transfer is waiting for the required business approvals."
                    : "The business transfer has been created successfully."));

        await _transferRiskService.AssessAsync(transfer, userId, ct);
        await _screeningService.ScreenTransferAsync(transfer, userId, ct);
        await _transactionMonitoringService.MonitorAsync(transfer, userId, ct);

        quote.IsUsed = true;
        quote.UsedAt = DateTime.UtcNow;
        quote.LastUpdatedAt = DateTime.UtcNow;
        quote.LastUpdatedByUserId = userId;

        if (approvalRequired)
        {
            await _notifications.QueueBusinessAsync(
                access.BusinessProfileId,
                "Business transfer approval required",
                $"Transfer {transfer.Reference} requires {transfer.RequiredApprovals} approval(s).",
                "Transfer",
                transfer.Id,
                ct);
        }
        else if (transfer.BusinessFundingSource == BusinessFundingSource.BusinessWallet)
        {
            await _fundingService.ReserveTransferAsync(
                transfer,
                userId,
                "BusinessTransfer",
                null,
                ct);
        }
        else
        {
            await _notifications.QueueBusinessAsync(
                access.BusinessProfileId,
                "Business transfer awaiting funding",
                $"Transfer {transfer.Reference} is approved and waiting for external funding.",
                "Transfer",
                transfer.Id,
                ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetTransferAsync(userId, transfer.Id, ct);
    }

    public async Task<BusinessTransferDetailsDto> GetTransferAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewTransfers, ct);
        var transfer = await BusinessTransferQuery(access.BusinessProfileId)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transferId, ct)
            ?? throw new InvalidOperationException("Business transfer not found.");

        return await ToDetailsDtoAsync(transfer, ct);
    }

    public async Task<PagedResult<BusinessTransferDto>> GetTransfersAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewTransfers, ct);
        return await _db.Transfers
            .AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BusinessTransferDto(
                x.Id,
                x.Reference,
                x.BusinessProfileId!.Value,
                x.BusinessBeneficiaryId!.Value,
                x.BusinessBeneficiary!.Name,
                x.BusinessBeneficiaryBankAccountId,
                x.BusinessBeneficiaryMobileWalletId,
                x.TransferQuoteId,
                x.TransferType,
                x.Purpose,
                x.PurposeNote,
                x.SourceCountryCode,
                x.DestinationCountryCode,
                x.SourceCurrencyCode,
                x.DestinationCurrencyCode,
                x.SourceAmount,
                x.DestinationAmount,
                x.FeeAmount,
                x.TotalPayableAmount,
                x.CustomerRate,
                x.Status,
                x.BusinessFundingSource ?? BusinessFundingSource.BusinessWallet,
                x.ApprovalStatus,
                x.RequiredApprovals,
                x.ApprovalCount,
                x.RequestedByUserId,
                x.SubmittedForApprovalAt,
                x.ApprovedAt,
                x.RejectedAt,
                x.ApprovalRejectionReason,
                x.ProviderTransferId,
                x.ProviderReference,
                x.CreatedAt,
                x.LastUpdatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<BusinessTransferDetailsDto> ApproveAsync(
        Guid userId,
        Guid transferId,
        BusinessTransferDecisionRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ApproveTransfers, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var transfer = await BusinessTransferQuery(access.BusinessProfileId)
            .FirstOrDefaultAsync(x => x.Id == transferId, ct)
            ?? throw new InvalidOperationException("Business transfer not found.");

        if (transfer.ApprovalStatus != BusinessApprovalStatus.Pending || transfer.Status != TransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only a transfer pending approval can be approved.");
        }

        if (!access.AllowTransferCreatorApproval && transfer.RequestedByUserId == userId)
        {
            throw new InvalidOperationException("The transfer creator cannot approve this transfer.");
        }

        var alreadyActioned = await _db.BusinessApprovals.AnyAsync(x =>
            x.TransferId == transfer.Id &&
            x.ActionedByUserId == userId,
            ct);
        if (alreadyActioned)
        {
            throw new InvalidOperationException("This user has already actioned the transfer approval.");
        }

        _db.BusinessApprovals.Add(new BusinessApproval
        {
            BusinessProfileId = access.BusinessProfileId,
            TransferId = transfer.Id,
            ActionedByUserId = userId,
            Action = BusinessApprovalAction.Approved,
            Comment = Clean(request.Comment, 1000)
        });
        transfer.ApprovalCount++;
        transfer.LastUpdatedAt = DateTime.UtcNow;
        transfer.LastUpdatedByUserId = userId;

        if (transfer.ApprovalCount >= transfer.RequiredApprovals)
        {
            transfer.ApprovalStatus = BusinessApprovalStatus.Approved;
            transfer.ApprovedAt = DateTime.UtcNow;
            transfer.FinalApprovedByUserId = userId;

            _transferStatusService.ApplyTransition(
                transfer,
                TransferStatus.PendingPayment,
                new TransferStatusTransitionContext(
                    Source: "BusinessApproval",
                    Reason: "The required business approvals were completed.",
                    ChangedByUserId: userId,
                    EventType: "BUSINESS_TRANSFER_APPROVED",
                    Title: "Transfer approved",
                    Description: "The transfer has been approved and is waiting for funding confirmation."));

            if (transfer.BusinessFundingSource == BusinessFundingSource.BusinessWallet)
            {
                await _fundingService.ReserveTransferAsync(
                    transfer,
                    userId,
                    "BusinessApproval",
                    null,
                    ct);
            }

            await _notifications.QueueBusinessAsync(
                access.BusinessProfileId,
                "Business transfer approved",
                transfer.BusinessFundingSource == BusinessFundingSource.BusinessWallet
                    ? $"Transfer {transfer.Reference} was approved and funded from the business wallet."
                    : $"Transfer {transfer.Reference} was approved and is waiting for external funding.",
                "Transfer",
                transfer.Id,
                ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetTransferAsync(userId, transfer.Id, ct);
    }

    public async Task<BusinessTransferDetailsDto> RejectAsync(
        Guid userId,
        Guid transferId,
        BusinessTransferDecisionRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ApproveTransfers, ct);
        var comment = Clean(request.Comment, 1000) ?? "Rejected by an authorized business approver.";

        var transfer = await BusinessTransferQuery(access.BusinessProfileId)
            .FirstOrDefaultAsync(x => x.Id == transferId, ct)
            ?? throw new InvalidOperationException("Business transfer not found.");

        if (transfer.ApprovalStatus != BusinessApprovalStatus.Pending || transfer.Status != TransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only a transfer pending approval can be rejected.");
        }

        if (!access.AllowTransferCreatorApproval && transfer.RequestedByUserId == userId)
        {
            throw new InvalidOperationException("The transfer creator cannot reject this transfer.");
        }

        var alreadyActioned = await _db.BusinessApprovals.AnyAsync(x =>
            x.TransferId == transfer.Id &&
            x.ActionedByUserId == userId,
            ct);
        if (alreadyActioned)
        {
            throw new InvalidOperationException("This user has already actioned the transfer approval.");
        }

        _db.BusinessApprovals.Add(new BusinessApproval
        {
            BusinessProfileId = access.BusinessProfileId,
            TransferId = transfer.Id,
            ActionedByUserId = userId,
            Action = BusinessApprovalAction.Rejected,
            Comment = comment
        });

        transfer.ApprovalStatus = BusinessApprovalStatus.Rejected;
        transfer.RejectedAt = DateTime.UtcNow;
        transfer.RejectedByUserId = userId;
        transfer.ApprovalRejectionReason = comment;

        _transferStatusService.ApplyTransition(
            transfer,
            TransferStatus.Rejected,
            new TransferStatusTransitionContext(
                Source: "BusinessApproval",
                Reason: comment,
                ChangedByUserId: userId,
                EventType: "BUSINESS_TRANSFER_REJECTED",
                Title: "Transfer rejected",
                Description: comment));

        _fundingService.ReleaseTransferReservation(
            transfer,
            comment,
            userId,
            "BusinessApproval");

        await _notifications.QueueBusinessAsync(
            access.BusinessProfileId,
            "Business transfer rejected",
            $"Transfer {transfer.Reference} was rejected. {comment}",
            "Transfer",
            transfer.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        return await GetTransferAsync(userId, transfer.Id, ct);
    }

    public async Task<BusinessTransferReportDto> GetReportAsync(
        Guid userId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewReports, ct);
        var query = _db.Transfers.AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted);

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value.ToUniversalTime());
        }
        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt < to.Value.ToUniversalTime().AddDays(1));
        }

        var rows = await query.Select(x => new
        {
            x.Status,
            x.ApprovalStatus,
            x.SourceCurrencyCode,
            x.DestinationCurrencyCode,
            x.SourceAmount,
            x.DestinationAmount
        }).ToListAsync(ct);

        return new BusinessTransferReportDto(
            rows.Count,
            rows.Count(x => x.ApprovalStatus == BusinessApprovalStatus.Pending),
            rows.Count(x => x.Status is TransferStatus.PaymentReceived or TransferStatus.Processing or TransferStatus.PayoutInitiated),
            rows.Count(x => x.Status == TransferStatus.Completed),
            rows.Count(x => x.Status is TransferStatus.Failed or TransferStatus.Rejected),
            rows.Sum(x => x.SourceAmount),
            rows.Sum(x => x.DestinationAmount),
            rows.GroupBy(x => x.SourceCurrencyCode).ToDictionary(x => x.Key, x => x.Sum(y => y.SourceAmount)),
            rows.GroupBy(x => x.DestinationCurrencyCode).ToDictionary(x => x.Key, x => x.Sum(y => y.DestinationAmount)));
    }

    private IQueryable<Transfer> BusinessTransferQuery(Guid businessProfileId) =>
        _db.Transfers
            .Include(x => x.BusinessBeneficiary)
            .Include(x => x.BusinessBeneficiaryBankAccount)
            .Include(x => x.BusinessBeneficiaryMobileWallet)
            .Include(x => x.TimelineEvents)
            .Where(x => x.BusinessProfileId == businessProfileId && !x.IsDeleted);

    private async Task<BusinessTransferDetailsDto> ToDetailsDtoAsync(Transfer transfer, CancellationToken ct)
    {
        var approvals = await _db.BusinessApprovals.AsNoTracking()
            .Where(x => x.TransferId == transfer.Id)
            .OrderBy(x => x.ActionedAt)
            .Select(x => new BusinessApprovalDto(x.Id, x.ActionedByUserId, x.Action, x.Comment, x.ActionedAt))
            .ToListAsync(ct);

        return new BusinessTransferDetailsDto(
            ToDto(transfer),
            approvals,
            transfer.TimelineEvents.OrderBy(x => x.OccurredAt)
                .Select(x => new BusinessTransferTimelineDto(x.EventType, x.Title, x.Description, x.OccurredAt))
                .ToList());
    }

    private static BusinessTransferDto ToDto(Transfer x) =>
        new(
            x.Id,
            x.Reference,
            x.BusinessProfileId!.Value,
            x.BusinessBeneficiaryId!.Value,
            x.BusinessBeneficiary?.Name ?? "",
            x.BusinessBeneficiaryBankAccountId,
            x.BusinessBeneficiaryMobileWalletId,
            x.TransferQuoteId,
            x.TransferType,
            x.Purpose,
            x.PurposeNote,
            x.SourceCountryCode,
            x.DestinationCountryCode,
            x.SourceCurrencyCode,
            x.DestinationCurrencyCode,
            x.SourceAmount,
            x.DestinationAmount,
            x.FeeAmount,
            x.TotalPayableAmount,
            x.CustomerRate,
            x.Status,
            x.BusinessFundingSource ?? BusinessFundingSource.BusinessWallet,
            x.ApprovalStatus,
            x.RequiredApprovals,
            x.ApprovalCount,
            x.RequestedByUserId,
            x.SubmittedForApprovalAt,
            x.ApprovedAt,
            x.RejectedAt,
            x.ApprovalRejectionReason,
            x.ProviderTransferId,
            x.ProviderReference,
            x.CreatedAt,
            x.LastUpdatedAt);

    private async Task<ExchangeRate> GetRateAsync(string sourceCurrency, string destinationCurrency, CancellationToken ct) =>
        await _db.ExchangeRates.AsNoTracking()
            .Where(x =>
                x.SourceCurrencyCode == sourceCurrency &&
                x.DestinationCurrencyCode == destinationCurrency &&
                x.IsActive &&
                x.EffectiveFrom <= DateTime.UtcNow &&
                (x.EffectiveTo == null || x.EffectiveTo > DateTime.UtcNow))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("Exchange rate is not available for this currency pair.");

    private async Task<TransferFee> GetFeeAsync(
        string sourceCountry,
        string destinationCountry,
        string sourceCurrency,
        string destinationCurrency,
        TransferType transferType,
        decimal amount,
        CancellationToken ct) =>
        await _db.TransferFees.AsNoTracking()
            .Where(x =>
                x.SourceCountryCode == sourceCountry &&
                x.DestinationCountryCode == destinationCountry &&
                x.SourceCurrencyCode == sourceCurrency &&
                x.DestinationCurrencyCode == destinationCurrency &&
                x.TransferType == transferType &&
                x.IsActive &&
                amount >= x.MinAmount &&
                (x.MaxAmount == null || amount <= x.MaxAmount))
            .OrderByDescending(x => x.MinAmount)
            .FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("Transfer fee is not configured for this business corridor.");

    private async Task EnsureCorridorAsync(string countryCode, string currencyCode, bool sending, CancellationToken ct)
    {
        var exists = await _db.CountryAssets.AsNoTracking().AnyAsync(x =>
            x.CountryCode == countryCode &&
            x.AssetCode == currencyCode &&
            x.Country.IsSupported &&
            x.Asset.IsSupported &&
            (sending
                ? x.CanSend && x.Country.IsSendCountry
                : x.CanReceive && x.Country.IsReceiveCountry),
            ct);
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Currency '{currencyCode}' is not supported for {(sending ? "sending from" : "receiving in")} country '{countryCode}'.");
        }
    }

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

    private static bool RequiresApproval(BusinessAccessContext access, decimal totalPayable) =>
        access.RequiresTransferApproval &&
        (!access.TransferApprovalThreshold.HasValue || totalPayable >= access.TransferApprovalThreshold.Value);

    private static void ValidateQuote(TransferQuote quote)
    {
        if (quote.IsUsed) throw new InvalidOperationException("Transfer quote has already been used.");
        if (quote.IsExpired) throw new InvalidOperationException("Transfer quote has expired. Create a new quote.");
        if (quote.TransferType is not TransferType.BusinessToConsumer and not TransferType.BusinessToBusiness)
            throw new InvalidOperationException("The selected quote is not a business transfer quote.");
    }

    private static void ValidateBeneficiaryType(BusinessBeneficiaryType beneficiaryType, TransferType transferType)
    {
        if (beneficiaryType == BusinessBeneficiaryType.Business && transferType != TransferType.BusinessToBusiness)
            throw new InvalidOperationException("A business beneficiary requires a BusinessToBusiness quote.");
        if (beneficiaryType == BusinessBeneficiaryType.Individual && transferType != TransferType.BusinessToConsumer)
            throw new InvalidOperationException("An individual beneficiary requires a BusinessToConsumer quote.");
    }

    private static void ValidateDestinationSelection(Guid? bankAccountId, Guid? mobileWalletId)
    {
        if (bankAccountId.HasValue == mobileWalletId.HasValue)
            throw new InvalidOperationException("Select exactly one beneficiary bank account or mobile wallet.");
    }

    private static void ValidateDestination(
        string beneficiaryCountry,
        TransferQuote quote,
        BusinessBeneficiaryBankAccount? bankAccount,
        BusinessBeneficiaryMobileWallet? mobileWallet)
    {
        if (!string.Equals(beneficiaryCountry, quote.DestinationCountryCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The beneficiary country does not match the quote destination country.");

        if (bankAccount is not null)
        {
            if (!string.Equals(bankAccount.CountryCode, quote.DestinationCountryCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(bankAccount.CurrencyCode, quote.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The bank account does not match the quote destination corridor.");
            if (!bankAccount.IsVerified)
                throw new InvalidOperationException("The beneficiary bank account must be verified before transfer creation.");
        }

        if (mobileWallet is not null)
        {
            if (!string.Equals(mobileWallet.CountryCode, quote.DestinationCountryCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(mobileWallet.CurrencyCode, quote.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The mobile wallet does not match the quote destination corridor.");
            if (!mobileWallet.IsVerified)
                throw new InvalidOperationException("The beneficiary mobile wallet must be verified before transfer creation.");
        }
    }

    private async Task<string> GenerateUniqueReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateTransferReference();
            if (!await _db.Transfers.AsNoTracking().AnyAsync(x => x.Reference == reference, ct)) return reference;
        }
        throw new InvalidOperationException("Unable to generate a unique transfer reference.");
    }

    private static TransferQuoteDto ToQuoteDto(TransferQuote quote) => new()
    {
        Id = quote.Id,
        CustomerProfileId = quote.CustomerProfileId,
        BusinessProfileId = quote.BusinessProfileId,
        SourceCountryCode = quote.SourceCountryCode,
        DestinationCountryCode = quote.DestinationCountryCode,
        SourceCurrencyCode = quote.SourceCurrencyCode,
        DestinationCurrencyCode = quote.DestinationCurrencyCode,
        TransferType = quote.TransferType,
        SourceAmount = quote.SourceAmount,
        DestinationAmount = quote.DestinationAmount,
        ProviderRate = quote.ProviderRate,
        CustomerRate = quote.CustomerRate,
        FeeAmount = quote.FeeAmount,
        FeeCurrencyCode = quote.FeeCurrencyCode,
        TotalPayableAmount = quote.TotalPayableAmount,
        ProviderCode = quote.ProviderCode,
        ProviderQuoteId = quote.ProviderQuoteId,
        ExpiresAt = quote.ExpiresAt,
        IsUsed = quote.IsUsed,
        IsExpired = quote.IsExpired,
        CreatedAt = quote.CreatedAt
    };

    private static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Country and currency codes are required.");

        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 10)
            throw new InvalidOperationException("Country and currency codes cannot exceed 10 characters.");

        return code;
    }

    private static string? Clean(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (maxLength.HasValue && cleaned.Length > maxLength.Value)
            throw new InvalidOperationException($"Value cannot exceed {maxLength.Value} characters.");
        return cleaned;
    }
}
