using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Dtos.Payments;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.BusinessFunding;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Audit;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.Notifications;
using KorridorX.Services.Payments;
using KorridorX.Services.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.BusinessFunding;

public class BusinessFundingService : IBusinessFundingService
{
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _accessService;
    private readonly ITransferStatusService _transferStatusService;
    private readonly ICollectionPaymentMethodPolicy _paymentMethodPolicy;
    private readonly ICollectionStatusService _collectionStatusService;
    private readonly IRemittanceProvider _provider;
    private readonly IComplianceGateService _complianceGateService;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _auditService;
    private readonly IReadOnlyDictionary<string, string> _collectionWalletIds;

    public BusinessFundingService(
        AppDbContext db,
        IBusinessAccessService accessService,
        ITransferStatusService transferStatusService,
        ICollectionPaymentMethodPolicy paymentMethodPolicy,
        ICollectionStatusService collectionStatusService,
        IRemittanceProvider provider,
        IComplianceGateService complianceGateService,
        INotificationQueueService notifications,
        IAuditService auditService,
        IOptions<BlaaizOptions> blaaizOptions)
    {
        _db = db;
        _accessService = accessService;
        _transferStatusService = transferStatusService;
        _paymentMethodPolicy = paymentMethodPolicy;
        _collectionStatusService = collectionStatusService;
        _provider = provider;
        _complianceGateService = complianceGateService;
        _notifications = notifications;
        _auditService = auditService;
        _collectionWalletIds = blaaizOptions.Value.CollectionWalletIds;
    }

    public async Task<PagedResult<BusinessWalletDto>> GetWalletsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewWallets, ct);

        return await _db.BusinessWallets
            .AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted)
            .OrderBy(x => x.CurrencyCode)
            .Select(x => new BusinessWalletDto(
                x.Id,
                x.BusinessProfileId,
                x.CurrencyCode,
                x.Status,
                x.SettledBalance,
                x.AvailableBalance,
                x.HeldBalance,
                x.CreatedAt,
                x.LastUpdatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<BusinessLedgerTransactionDto>> GetLedgerAsync(
        Guid userId,
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewWallets, ct);
        var walletExists = await _db.BusinessWallets.AsNoTracking().AnyAsync(x =>
            x.Id == walletId &&
            x.BusinessProfileId == access.BusinessProfileId &&
            !x.IsDeleted,
            ct);

        if (!walletExists)
        {
            throw new InvalidOperationException("Business wallet not found.");
        }

        var paged = await _db.BusinessLedgerTransactions
            .AsNoTracking()
            .Include(x => x.Entries)
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.Entries.Any(y => y.BusinessWalletId == walletId) &&
                !x.IsDeleted)
            .OrderByDescending(x => x.PostedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<BusinessLedgerTransactionDto>
        {
            Items = paged.Items.Select(x => new BusinessLedgerTransactionDto(
                x.Id,
                x.Reference,
                x.CurrencyCode,
                x.Type,
                x.Status,
                x.Amount,
                x.Description,
                x.TransferId,
                x.BusinessPaymentBatchId,
                x.CollectionId,
                x.PostedAt,
                x.Entries.OrderBy(y => y.CreatedAt)
                    .Select(y => new BusinessLedgerEntryDto(
                        y.Id,
                        y.AccountType,
                        y.Side,
                        y.Amount,
                        y.AccountBalanceAfter))
                    .ToList(),
                x.ReversalOfTransactionId,
                x.ReversedByTransactionId,
                x.ReversedAt,
                x.ReversalReason)).ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<BusinessWalletDto> CreditWalletAsync(
        Guid adminUserId,
        AdminCreditBusinessWalletRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new InvalidOperationException("Credit amount must be greater than zero.");

        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        var reason = Clean(request.Reason, 1000)
            ?? throw new InvalidOperationException("A credit reason is required.");
        var requestHash = ComputeRequestHash(new
        {
            request.BusinessProfileId,
            CurrencyCode = currencyCode,
            request.Amount,
            Reason = reason,
            Operation = "BusinessWalletCredit"
        });

        var existing = await FindIdempotentLedgerTransactionAsync(
            request.BusinessProfileId,
            normalizedKey,
            requestHash,
            ct);
        if (existing is not null)
        {
            var existingWallet = await _db.BusinessWallets.AsNoTracking()
                .FirstAsync(x =>
                    x.BusinessProfileId == request.BusinessProfileId &&
                    x.CurrencyCode == currencyCode &&
                    !x.IsDeleted,
                    ct);
            return ToWalletDto(existingWallet);
        }

        var businessExists = await _db.BusinessProfiles.AnyAsync(x =>
            x.Id == request.BusinessProfileId && !x.IsDeleted,
            ct);
        if (!businessExists)
            throw new InvalidOperationException("Business profile not found.");

        var currencySupported = await _db.Currencies.AsNoTracking().AnyAsync(x =>
            x.Code == currencyCode && x.IsSupported,
            ct);
        if (!currencySupported)
            throw new InvalidOperationException($"Currency '{currencyCode}' is not supported.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var wallet = await GetOrCreateWalletAsync(request.BusinessProfileId, currencyCode, adminUserId, ct);
        EnsureWalletNotClosed(wallet);

        var oldBalances = Snapshot(wallet);
        wallet.SettledBalance += request.Amount;
        wallet.AvailableBalance += request.Amount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = adminUserId;

        PostIdempotentLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.FundingCredit,
            request.Amount,
            reason,
            adminUserId,
            normalizedKey,
            requestHash,
            null,
            null,
            null,
            (BusinessLedgerAccountType.External, BusinessLedgerEntrySide.Debit, null),
            (BusinessLedgerAccountType.Available, BusinessLedgerEntrySide.Credit, wallet.AvailableBalance));

        _auditService.Stage(new AuditRecordRequest(
            Action: "BUSINESS_WALLET_CREDITED",
            Category: "BusinessFunding",
            EntityName: nameof(BusinessWallet),
            EntityId: wallet.Id.ToString(),
            OldValues: oldBalances,
            NewValues: Snapshot(wallet),
            Metadata: new { reason, idempotencyKey = normalizedKey },
            UserId: adminUserId));

        await _notifications.QueueBusinessAsync(
            request.BusinessProfileId,
            "Business wallet credited",
            $"Your {currencyCode} business wallet was credited with {request.Amount:N2} {currencyCode}. {reason}",
            "BusinessWallet",
            wallet.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToWalletDto(wallet);
    }

    public async Task<BusinessWalletDto> SetWalletStatusAsync(
        Guid adminUserId,
        Guid walletId,
        BusinessWalletStatus status,
        string reason,
        CancellationToken ct = default)
    {
        if (status is not (BusinessWalletStatus.Active or BusinessWalletStatus.Frozen))
            throw new InvalidOperationException("Administrative wallet status changes support only Active or Frozen.");

        var cleanReason = Clean(reason, 1000)
            ?? throw new InvalidOperationException("A wallet status reason is required.");
        var wallet = await _db.BusinessWallets
            .FirstOrDefaultAsync(x => x.Id == walletId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business wallet not found.");

        if (wallet.Status == BusinessWalletStatus.Closed)
            throw new InvalidOperationException("A closed business wallet cannot be reopened or frozen.");
        if (wallet.Status == status)
            return ToWalletDto(wallet);

        var oldStatus = wallet.Status;
        wallet.Status = status;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = adminUserId;

        _auditService.Stage(new AuditRecordRequest(
            Action: status == BusinessWalletStatus.Frozen
                ? "BUSINESS_WALLET_FROZEN"
                : "BUSINESS_WALLET_UNFROZEN",
            Category: "BusinessFunding",
            EntityName: nameof(BusinessWallet),
            EntityId: wallet.Id.ToString(),
            OldValues: new { Status = oldStatus },
            NewValues: new { wallet.Status },
            Metadata: new { reason = cleanReason },
            UserId: adminUserId));

        await _notifications.QueueBusinessAsync(
            wallet.BusinessProfileId,
            status == BusinessWalletStatus.Frozen ? "Business wallet frozen" : "Business wallet reactivated",
            $"Your {wallet.CurrencyCode} business wallet status changed from {oldStatus} to {status}. {cleanReason}",
            "BusinessWallet",
            wallet.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        return ToWalletDto(wallet);
    }

    public async Task<BusinessWalletDto> AdjustWalletAsync(
        Guid adminUserId,
        Guid walletId,
        AdminAdjustBusinessWalletRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new InvalidOperationException("Adjustment amount must be greater than zero.");

        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var reason = Clean(request.Reason, 1000)
            ?? throw new InvalidOperationException("An adjustment reason is required.");
        var wallet = await _db.BusinessWallets
            .FirstOrDefaultAsync(x => x.Id == walletId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business wallet not found.");
        EnsureWalletNotClosed(wallet);

        var requestHash = ComputeRequestHash(new
        {
            walletId,
            request.Amount,
            request.Direction,
            Reason = reason,
            Operation = "BusinessWalletAdjustment"
        });
        var existing = await FindIdempotentLedgerTransactionAsync(
            wallet.BusinessProfileId,
            normalizedKey,
            requestHash,
            ct);
        if (existing is not null)
            return ToWalletDto(wallet);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var oldBalances = Snapshot(wallet);
        (BusinessLedgerAccountType Account, BusinessLedgerEntrySide Side, decimal? BalanceAfter) first;
        (BusinessLedgerAccountType Account, BusinessLedgerEntrySide Side, decimal? BalanceAfter) second;

        if (request.Direction == BusinessWalletAdjustmentDirection.Credit)
        {
            wallet.SettledBalance += request.Amount;
            wallet.AvailableBalance += request.Amount;
            first = (BusinessLedgerAccountType.External, BusinessLedgerEntrySide.Debit, null);
            second = (BusinessLedgerAccountType.Available, BusinessLedgerEntrySide.Credit, wallet.AvailableBalance);
        }
        else if (request.Direction == BusinessWalletAdjustmentDirection.Debit)
        {
            if (wallet.AvailableBalance < request.Amount || wallet.SettledBalance < request.Amount)
                throw new InvalidOperationException("The wallet does not have enough available settled funds for this adjustment.");

            wallet.SettledBalance -= request.Amount;
            wallet.AvailableBalance -= request.Amount;
            first = (BusinessLedgerAccountType.Available, BusinessLedgerEntrySide.Debit, wallet.AvailableBalance);
            second = (BusinessLedgerAccountType.External, BusinessLedgerEntrySide.Credit, null);
        }
        else
        {
            throw new InvalidOperationException("Unsupported wallet adjustment direction.");
        }

        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = adminUserId;

        PostIdempotentLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.ManualAdjustment,
            request.Amount,
            reason,
            adminUserId,
            normalizedKey,
            requestHash,
            null,
            null,
            null,
            first,
            second);

        _auditService.Stage(new AuditRecordRequest(
            Action: "BUSINESS_WALLET_ADJUSTED",
            Category: "BusinessFunding",
            EntityName: nameof(BusinessWallet),
            EntityId: wallet.Id.ToString(),
            OldValues: oldBalances,
            NewValues: Snapshot(wallet),
            Metadata: new { request.Direction, request.Amount, reason, idempotencyKey = normalizedKey },
            UserId: adminUserId));

        await _notifications.QueueBusinessAsync(
            wallet.BusinessProfileId,
            "Business wallet adjusted",
            $"A {request.Direction.ToString().ToLowerInvariant()} adjustment of {request.Amount:N2} {wallet.CurrencyCode} was applied. {reason}",
            "BusinessWallet",
            wallet.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToWalletDto(wallet);
    }

    public async Task<BusinessLedgerTransactionDto> ReverseLedgerTransactionAsync(
        Guid adminUserId,
        Guid transactionId,
        string reason,
        CancellationToken ct = default)
    {
        var cleanReason = Clean(reason, 1000)
            ?? throw new InvalidOperationException("A reversal reason is required.");
        var original = await _db.BusinessLedgerTransactions
            .Include(x => x.Entries)
                .ThenInclude(x => x.BusinessWallet)
            .FirstOrDefaultAsync(x => x.Id == transactionId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business ledger transaction not found.");

        if (original.Status == BusinessLedgerTransactionStatus.Reversed || original.ReversedByTransactionId is not null)
            throw new InvalidOperationException("The ledger transaction has already been reversed.");
        if (original.Type is not (BusinessLedgerTransactionType.FundingCredit or BusinessLedgerTransactionType.ManualAdjustment))
            throw new InvalidOperationException("Only funding credits and manual adjustments can be reversed through this endpoint.");
        if (original.TransferId is not null || original.BusinessPaymentBatchId is not null || original.CollectionId is not null)
            throw new InvalidOperationException("Transactions linked to transfers, batches, or collections require their dedicated recovery workflow.");

        var walletEntry = original.Entries.SingleOrDefault(x => x.BusinessWalletId is not null);
        var externalEntry = original.Entries.SingleOrDefault(x => x.AccountType == BusinessLedgerAccountType.External);
        if (walletEntry?.BusinessWallet is null || externalEntry is null || walletEntry.AccountType != BusinessLedgerAccountType.Available)
            throw new InvalidOperationException("The ledger transaction is not eligible for automatic reversal.");

        var wallet = walletEntry.BusinessWallet;
        EnsureWalletNotClosed(wallet);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var oldBalances = Snapshot(wallet);

        BusinessLedgerEntrySide walletReversalSide;
        BusinessLedgerEntrySide externalReversalSide;
        if (walletEntry.Side == BusinessLedgerEntrySide.Credit)
        {
            if (wallet.AvailableBalance < original.Amount || wallet.SettledBalance < original.Amount)
                throw new InvalidOperationException("The wallet does not have enough available settled funds to reverse this credit.");

            wallet.AvailableBalance -= original.Amount;
            wallet.SettledBalance -= original.Amount;
            walletReversalSide = BusinessLedgerEntrySide.Debit;
            externalReversalSide = BusinessLedgerEntrySide.Credit;
        }
        else
        {
            wallet.AvailableBalance += original.Amount;
            wallet.SettledBalance += original.Amount;
            walletReversalSide = BusinessLedgerEntrySide.Credit;
            externalReversalSide = BusinessLedgerEntrySide.Debit;
        }

        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = adminUserId;

        var reversal = PostLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.Reversal,
            original.Amount,
            $"Reversal of {original.Reference}. {cleanReason}",
            adminUserId,
            null,
            null,
            null,
            (BusinessLedgerAccountType.Available, walletReversalSide, wallet.AvailableBalance),
            (BusinessLedgerAccountType.External, externalReversalSide, null));
        reversal.ReversalOfTransactionId = original.Id;

        var originalStatus = original.Status;
        original.Status = BusinessLedgerTransactionStatus.Reversed;
        original.ReversedByTransactionId = reversal.Id;
        original.ReversedAt = DateTime.UtcNow;
        original.ReversalReason = cleanReason;
        original.LastUpdatedAt = DateTime.UtcNow;
        original.LastUpdatedByUserId = adminUserId;

        _auditService.Stage(new AuditRecordRequest(
            Action: "BUSINESS_LEDGER_TRANSACTION_REVERSED",
            Category: "BusinessFunding",
            EntityName: nameof(BusinessLedgerTransaction),
            EntityId: original.Id.ToString(),
            OldValues: new { Status = originalStatus, OriginalBalances = oldBalances },
            NewValues: new { Status = BusinessLedgerTransactionStatus.Reversed, ReversalTransactionId = reversal.Id, Balances = Snapshot(wallet) },
            Metadata: new { reason = cleanReason, original.Reference },
            UserId: adminUserId));

        await _notifications.QueueBusinessAsync(
            wallet.BusinessProfileId,
            "Business wallet ledger reversal",
            $"Ledger transaction {original.Reference} was reversed for {original.Amount:N2} {original.CurrencyCode}. {cleanReason}",
            "BusinessLedgerTransaction",
            original.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToLedgerDto(reversal);
    }

    public async Task<BusinessTransferFundingDto> GetTransferFundingAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewWallets, ct);
        var transfer = await _db.Transfers.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == transferId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business transfer not found.");

        var reservation = await _db.BusinessWalletReservations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransferId == transfer.Id && !x.IsDeleted, ct);
        var collection = await _db.Collections.AsNoTracking()
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.TransferId == transfer.Id && !x.IsDeleted, ct);

        return new BusinessTransferFundingDto(
            transfer.Id,
            transfer.Reference,
            transfer.Status,
            transfer.BusinessFundingSource ?? BusinessFundingSource.BusinessWallet,
            transfer.SourceCurrencyCode,
            transfer.TotalPayableAmount,
            reservation?.BusinessWalletId,
            reservation?.Status,
            collection is null ? null : ToCollectionDetailsDto(collection));
    }

    public async Task<BusinessTransferFundingDto> FundTransferFromWalletAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageFunding, ct);
        var transfer = await _db.Transfers
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x =>
                x.Id == transferId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business transfer not found.");

        await ReserveTransferAsync(transfer, userId, "BusinessFunding", null, ct);
        await _db.SaveChangesAsync(ct);

        var reservation = await _db.BusinessWalletReservations
            .AsNoTracking()
            .Include(x => x.BusinessWallet)
            .FirstAsync(x => x.TransferId == transfer.Id && !x.IsDeleted, ct);

        return new BusinessTransferFundingDto(
            transfer.Id,
            transfer.Reference,
            transfer.Status,
            transfer.BusinessFundingSource ?? BusinessFundingSource.BusinessWallet,
            transfer.SourceCurrencyCode,
            transfer.TotalPayableAmount,
            reservation.BusinessWalletId,
            reservation.Status,
            null);
    }

    public async Task<CollectionDetailsDto> CreateExternalCollectionAsync(
        Guid userId,
        Guid transferId,
        CreateBusinessExternalCollectionRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageFunding, ct);
        var transfer = await _db.Transfers
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x =>
                x.Id == transferId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business transfer not found.");

        ValidateExternalCollectionTransfer(transfer);

        var existing = await _db.Collections
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.TransferId == transfer.Id && !x.IsDeleted, ct);
        if (existing is not null)
        {
            return ToCollectionDetailsDto(existing);
        }

        if (!_paymentMethodPolicy.IsSupported(
                transfer.SourceCountryCode,
                transfer.SourceCurrencyCode,
                request.PaymentMethod))
        {
            throw new InvalidOperationException(
                $"Payment method '{request.PaymentMethod}' is not supported for {transfer.SourceCountryCode}/{transfer.SourceCurrencyCode}.");
        }

        var collection = new Collection
        {
            TransferId = transfer.Id,
            Transfer = transfer,
            Reference = await GenerateUniqueCollectionReferenceAsync(ct),
            CurrencyCode = transfer.SourceCurrencyCode,
            Amount = transfer.TotalPayableAmount,
            PaymentMethod = request.PaymentMethod,
            Status = CollectionStatus.Pending,
            ProviderCode = transfer.ProviderCode,
            CreatedByUserId = userId
        };

        collection.Attempts.Add(new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = JsonSerializer.Serialize(new
            {
                businessProfileId = access.BusinessProfileId,
                transferId = transfer.Id,
                transferReference = transfer.Reference,
                collectionReference = collection.Reference,
                amount = collection.Amount,
                currencyCode = collection.CurrencyCode,
                paymentMethod = request.PaymentMethod.ToString()
            })
        });

        _db.Collections.Add(collection);
        await _notifications.QueueBusinessAsync(
            access.BusinessProfileId,
            "Business transfer funding requested",
            $"External funding was selected for transfer {transfer.Reference}.",
            "Transfer",
            transfer.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        return ToCollectionDetailsDto(collection);
    }

    public async Task<CollectionDetailsDto> InitiateExternalCollectionAsync(
        Guid userId,
        Guid collectionId,
        InitiateCollectionRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageFunding, ct);
        var collection = await _db.Collections
            .Include(x => x.Transfer)
            .ThenInclude(x => x.BusinessProfile)
            .ThenInclude(x => x!.OwnerUser)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionId &&
                x.Transfer.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business collection not found.");

        ValidateExternalCollectionTransfer(collection.Transfer);
        if (collection.Status is CollectionStatus.Initiated or CollectionStatus.Processing or CollectionStatus.Successful)
        {
            return ToCollectionDetailsDto(collection);
        }

        if (collection.PaymentMethod is not PaymentMethod.Card and not PaymentMethod.Interac)
        {
            throw new InvalidOperationException(
                $"Live provider initiation is not yet supported for '{collection.PaymentMethod}'.");
        }

        var business = collection.Transfer.BusinessProfile
            ?? throw new InvalidOperationException("Business profile is missing from the transfer.");
        var compliance = await _complianceGateService.EnsureBusinessCanInitiateMoneyMovementAsync(
            business.Id,
            _provider.ProviderCode,
            ct);

        var email = request.PayerEmail ?? business.ContactEmail ?? business.OwnerUser.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("A business payer email is required.");
        }

        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? business.BusinessName
            : request.CustomerName.Trim();
        var walletId = ResolveCollectionWalletId(collection.CurrencyCode);
        var cardRequest = request.Card;
        var card = cardRequest is null
            ? null
            : new RemittanceCardDetails(
                cardRequest.CardHolderName,
                cardRequest.CardNumber,
                cardRequest.Expiry,
                cardRequest.Cvc);

        var sanitizedRequest = JsonSerializer.Serialize(new
        {
            businessProfileId = business.Id,
            collectionId = collection.Id,
            transferId = collection.TransferId,
            amount = collection.Amount,
            currencyCode = collection.CurrencyCode,
            paymentMethod = collection.PaymentMethod.ToString(),
            payerEmail = email,
            customerName,
            redirectUrl = request.RedirectUrl,
            interacExpiryHours = request.InteracExpiryHours,
            card = card is null ? null : new
            {
                cardHolderName = card.CardHolderName,
                cardNumber = MaskCardNumber(card.CardNumber),
                expiry = card.Expiry,
                cvc = "***REDACTED***"
            }
        });

        var attempt = new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = sanitizedRequest,
            AttemptedAt = DateTime.UtcNow
        };
        collection.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await _provider.InitiateCollectionAsync(
                new RemittanceCollectionRequest(
                    collection.TransferId,
                    collection.Id,
                    collection.PaymentMethod,
                    collection.Amount,
                    collection.CurrencyCode,
                    compliance.ProviderCustomerId,
                    email,
                    customerName,
                    business.ContactPhone,
                    walletId,
                    request.RedirectUrl,
                    card,
                    request.InteracExpiryHours),
                ct);

            _collectionStatusService.ApplyTransition(
                collection,
                CollectionStatus.Initiated,
                new CollectionStatusTransitionContext(
                    Source: "BusinessProvider",
                    Reason: "Business collection was initiated with Blaaiz.",
                    ChangedByUserId: userId,
                    ProviderCollectionId: result.ProviderTransactionId,
                    ProviderReference: result.ProviderReference,
                    CheckoutUrl: result.CheckoutUrl,
                    ProviderExpiresAt: result.ExpiresAt,
                    ProviderRequestId: result.ProviderRequestLogId.ToString(),
                    ProviderResponseId: result.ProviderTransactionId,
                    RequestPayloadJson: sanitizedRequest,
                    ResponsePayloadJson: result.RawResponseJson,
                    MetadataJson: JsonSerializer.Serialize(new
                    {
                        businessProfileId = business.Id,
                        providerStatus = result.ProviderStatus
                    })),
                attempt);

            await UpsertProviderTransactionAsync(collection, result, ct);
            await _notifications.QueueBusinessAsync(
                business.Id,
                "Business funding initiated",
                $"Funding for transfer {collection.Transfer.Reference} has been initiated.",
                "Collection",
                collection.Id,
                ct);

            await _db.SaveChangesAsync(ct);
            return ToCollectionDetailsDto(collection);
        }
        catch (InvalidOperationException ex)
        {
            attempt.Status = ProviderRequestStatus.Failed;
            attempt.ErrorMessage = Clean(ex.Message, 1000);
            attempt.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
        catch (ProviderIntegrationException ex)
        {
            _collectionStatusService.ApplyTransition(
                collection,
                CollectionStatus.Failed,
                new CollectionStatusTransitionContext(
                    Source: "BusinessProvider",
                    Reason: ex.Message,
                    ChangedByUserId: userId,
                    ProviderRequestId: ex.RequestLogId?.ToString(),
                    RequestPayloadJson: sanitizedRequest,
                    ResponsePayloadJson: ex.ProviderResponse),
                attempt);
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task EnsureAvailableBalanceAsync(
        Guid businessProfileId,
        string currencyCode,
        decimal amount,
        CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Required funding amount must be greater than zero.");
        }

        var normalizedCurrency = NormalizeCode(currencyCode);
        var wallet = await _db.BusinessWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == businessProfileId &&
                x.CurrencyCode == normalizedCurrency &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                $"The business does not have a {normalizedCurrency} wallet.");

        EnsureWalletActive(wallet);
        if (wallet.AvailableBalance < amount)
        {
            throw new InvalidOperationException(
                $"Insufficient {normalizedCurrency} business-wallet balance. Available: {wallet.AvailableBalance:N2}; required: {amount:N2}.");
        }
    }

    public async Task ReserveTransferAsync(
        Transfer transfer,
        Guid? actionedByUserId,
        string source,
        Guid? businessPaymentBatchId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (!transfer.BusinessProfileId.HasValue)
        {
            throw new InvalidOperationException("Only business transfers can be funded from a business wallet.");
        }
        if (transfer.BusinessFundingSource != BusinessFundingSource.BusinessWallet)
        {
            throw new InvalidOperationException("This transfer is not configured to use the business wallet.");
        }
        if (transfer.Status == TransferStatus.PaymentReceived)
        {
            return;
        }
        if (transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"Business-wallet funding cannot be reserved while the transfer status is '{transfer.Status}'.");
        }
        if (transfer.ApprovalStatus is BusinessApprovalStatus.Pending or BusinessApprovalStatus.Rejected)
        {
            throw new InvalidOperationException("The transfer must complete its approval workflow before funding can be reserved.");
        }

        var existing = await _db.BusinessWalletReservations
            .Include(x => x.BusinessWallet)
            .FirstOrDefaultAsync(x => x.TransferId == transfer.Id && !x.IsDeleted, ct);
        if (existing is not null)
        {
            if (existing.Status is BusinessWalletReservationStatus.Active or BusinessWalletReservationStatus.Captured)
            {
                if (transfer.Status == TransferStatus.PendingPayment)
                {
                    ApplyWalletFundingTransition(transfer, source, actionedByUserId, existing.Reference);
                }
                return;
            }

            throw new InvalidOperationException("The previous wallet reservation for this transfer was released.");
        }

        var wallet = await _db.BusinessWallets.FirstOrDefaultAsync(x =>
            x.BusinessProfileId == transfer.BusinessProfileId.Value &&
            x.CurrencyCode == transfer.SourceCurrencyCode &&
            !x.IsDeleted,
            ct) ?? throw new InvalidOperationException(
                $"The business does not have a {transfer.SourceCurrencyCode} wallet.");

        EnsureWalletActive(wallet);
        if (wallet.AvailableBalance < transfer.TotalPayableAmount)
        {
            throw new InvalidOperationException(
                $"Insufficient {wallet.CurrencyCode} business-wallet balance. Available: {wallet.AvailableBalance:N2}; required: {transfer.TotalPayableAmount:N2}.");
        }

        wallet.AvailableBalance -= transfer.TotalPayableAmount;
        wallet.HeldBalance += transfer.TotalPayableAmount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;

        var reservation = new BusinessWalletReservation
        {
            BusinessWalletId = wallet.Id,
            BusinessWallet = wallet,
            TransferId = transfer.Id,
            Transfer = transfer,
            BusinessPaymentBatchId = businessPaymentBatchId,
            Reference = GenerateReference("KXRES"),
            Amount = transfer.TotalPayableAmount,
            Status = BusinessWalletReservationStatus.Active,
            ReservedAt = DateTime.UtcNow,
            CreatedByUserId = actionedByUserId
        };
        _db.BusinessWalletReservations.Add(reservation);

        PostLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.TransferReservation,
            reservation.Amount,
            $"Reserved funds for transfer {transfer.Reference}.",
            actionedByUserId,
            transfer.Id,
            businessPaymentBatchId,
            null,
            (BusinessLedgerAccountType.Available, BusinessLedgerEntrySide.Debit, wallet.AvailableBalance),
            (BusinessLedgerAccountType.Held, BusinessLedgerEntrySide.Credit, wallet.HeldBalance));

        ApplyWalletFundingTransition(transfer, source, actionedByUserId, reservation.Reference);
        await _notifications.QueueBusinessAsync(
            transfer.BusinessProfileId.Value,
            "Business transfer funded",
            $"{transfer.TotalPayableAmount:N2} {transfer.SourceCurrencyCode} was reserved for transfer {transfer.Reference}.",
            "Transfer",
            transfer.Id,
            ct);
    }

    public async Task ReactivateTransferReservationAsync(
        Transfer transfer,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default)
    {
        if (!transfer.BusinessProfileId.HasValue ||
            transfer.BusinessFundingSource != BusinessFundingSource.BusinessWallet)
        {
            return;
        }

        var reservation = await _db.BusinessWalletReservations
            .Include(x => x.BusinessWallet)
            .FirstOrDefaultAsync(x => x.TransferId == transfer.Id && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("The business-wallet reservation for this transfer was not found.");

        if (reservation.Status == BusinessWalletReservationStatus.Active)
        {
            return;
        }
        if (reservation.Status == BusinessWalletReservationStatus.Captured)
        {
            throw new InvalidOperationException("A captured business-wallet reservation cannot be reused.");
        }

        var wallet = reservation.BusinessWallet;
        EnsureWalletActive(wallet);
        if (wallet.AvailableBalance < reservation.Amount)
        {
            throw new InvalidOperationException(
                $"Insufficient {wallet.CurrencyCode} balance to retry the payout. Available: {wallet.AvailableBalance:N2}; required: {reservation.Amount:N2}.");
        }

        wallet.AvailableBalance -= reservation.Amount;
        wallet.HeldBalance += reservation.Amount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;
        reservation.Status = BusinessWalletReservationStatus.Active;
        reservation.ReservedAt = DateTime.UtcNow;
        reservation.ReleasedAt = null;
        reservation.ReleaseReason = null;
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.TransferReservation,
            reservation.Amount,
            $"Re-reserved funds for payout retry on transfer {transfer.Reference}. Source: {source}.",
            actionedByUserId,
            transfer.Id,
            reservation.BusinessPaymentBatchId,
            null,
            (BusinessLedgerAccountType.Available, BusinessLedgerEntrySide.Debit, wallet.AvailableBalance),
            (BusinessLedgerAccountType.Held, BusinessLedgerEntrySide.Credit, wallet.HeldBalance));
    }

    public bool CaptureTransferReservation(
        Transfer transfer,
        Guid? actionedByUserId,
        string source)
    {
        if (!transfer.BusinessProfileId.HasValue || transfer.BusinessFundingSource != BusinessFundingSource.BusinessWallet)
        {
            return false;
        }

        var reservation = _db.BusinessWalletReservations
            .Include(x => x.BusinessWallet)
            .FirstOrDefault(x => x.TransferId == transfer.Id && !x.IsDeleted);
        if (reservation is null || reservation.Status == BusinessWalletReservationStatus.Captured)
        {
            return false;
        }
        if (reservation.Status != BusinessWalletReservationStatus.Active)
        {
            return false;
        }

        var wallet = reservation.BusinessWallet;
        if (wallet.HeldBalance < reservation.Amount || wallet.SettledBalance < reservation.Amount)
        {
            throw new InvalidOperationException("Business-wallet reservation balances are inconsistent.");
        }

        wallet.HeldBalance -= reservation.Amount;
        wallet.SettledBalance -= reservation.Amount;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;
        reservation.Status = BusinessWalletReservationStatus.Captured;
        reservation.CapturedAt = DateTime.UtcNow;
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.ReservationCapture,
            reservation.Amount,
            $"Captured wallet reservation after payout for transfer {transfer.Reference}. Source: {source}.",
            actionedByUserId,
            transfer.Id,
            reservation.BusinessPaymentBatchId,
            null,
            (BusinessLedgerAccountType.Held, BusinessLedgerEntrySide.Debit, wallet.HeldBalance),
            (BusinessLedgerAccountType.Settlement, BusinessLedgerEntrySide.Credit, null));
        return true;
    }

    public bool ReleaseTransferReservation(
        Transfer transfer,
        string reason,
        Guid? actionedByUserId,
        string source)
    {
        if (!transfer.BusinessProfileId.HasValue || transfer.BusinessFundingSource != BusinessFundingSource.BusinessWallet)
        {
            return false;
        }

        var reservation = _db.BusinessWalletReservations
            .Include(x => x.BusinessWallet)
            .FirstOrDefault(x => x.TransferId == transfer.Id && !x.IsDeleted);
        if (reservation is null || reservation.Status == BusinessWalletReservationStatus.Released)
        {
            return false;
        }

        var wallet = reservation.BusinessWallet;
        BusinessLedgerAccountType debitAccount;
        decimal? debitBalanceAfter;

        if (reservation.Status == BusinessWalletReservationStatus.Active)
        {
            if (wallet.HeldBalance < reservation.Amount)
            {
                throw new InvalidOperationException("Business-wallet held balance is inconsistent.");
            }

            wallet.HeldBalance -= reservation.Amount;
            wallet.AvailableBalance += reservation.Amount;
            debitAccount = BusinessLedgerAccountType.Held;
            debitBalanceAfter = wallet.HeldBalance;
        }
        else
        {
            wallet.SettledBalance += reservation.Amount;
            wallet.AvailableBalance += reservation.Amount;
            debitAccount = BusinessLedgerAccountType.Settlement;
            debitBalanceAfter = null;
        }

        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actionedByUserId;
        reservation.Status = BusinessWalletReservationStatus.Released;
        reservation.ReleasedAt = DateTime.UtcNow;
        reservation.ReleaseReason = Clean(reason, 1000);
        reservation.LastUpdatedAt = DateTime.UtcNow;
        reservation.LastUpdatedByUserId = actionedByUserId;

        PostLedgerTransaction(
            wallet,
            BusinessLedgerTransactionType.ReservationRelease,
            reservation.Amount,
            $"Released or restored wallet funds for transfer {transfer.Reference}. Source: {source}. {reason}",
            actionedByUserId,
            transfer.Id,
            reservation.BusinessPaymentBatchId,
            null,
            (debitAccount, BusinessLedgerEntrySide.Debit, debitBalanceAfter),
            (BusinessLedgerAccountType.Available, BusinessLedgerEntrySide.Credit, wallet.AvailableBalance));
        return true;
    }

    private async Task<BusinessWallet> GetOrCreateWalletAsync(
        Guid businessProfileId,
        string currencyCode,
        Guid? userId,
        CancellationToken ct)
    {
        var wallet = await _db.BusinessWallets.FirstOrDefaultAsync(x =>
            x.BusinessProfileId == businessProfileId &&
            x.CurrencyCode == currencyCode &&
            !x.IsDeleted,
            ct);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new BusinessWallet
        {
            BusinessProfileId = businessProfileId,
            CurrencyCode = currencyCode,
            Status = BusinessWalletStatus.Active,
            CreatedByUserId = userId
        };
        _db.BusinessWallets.Add(wallet);
        return wallet;
    }

    private BusinessLedgerTransaction PostLedgerTransaction(
        BusinessWallet wallet,
        BusinessLedgerTransactionType type,
        decimal amount,
        string description,
        Guid? userId,
        Guid? transferId,
        Guid? batchId,
        Guid? collectionId,
        params (BusinessLedgerAccountType Account, BusinessLedgerEntrySide Side, decimal? BalanceAfter)[] entries)
    {
        if (entries.Length != 2 || entries[0].Side == entries[1].Side)
        {
            throw new InvalidOperationException("A business ledger transaction must contain one debit and one credit entry.");
        }

        var transaction = new BusinessLedgerTransaction
        {
            BusinessProfileId = wallet.BusinessProfileId,
            Reference = GenerateReference("KXLED"),
            CurrencyCode = wallet.CurrencyCode,
            Type = type,
            Status = BusinessLedgerTransactionStatus.Posted,
            Amount = amount,
            Description = Clean(description, 1000) ?? type.ToString(),
            TransferId = transferId,
            BusinessPaymentBatchId = batchId,
            CollectionId = collectionId,
            PostedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        foreach (var entry in entries)
        {
            transaction.Entries.Add(new BusinessLedgerEntry
            {
                BusinessLedgerTransactionId = transaction.Id,
                BusinessLedgerTransaction = transaction,
                BusinessWalletId = entry.Account == BusinessLedgerAccountType.External ? null : wallet.Id,
                BusinessWallet = entry.Account == BusinessLedgerAccountType.External ? null : wallet,
                AccountType = entry.Account,
                Side = entry.Side,
                Amount = amount,
                AccountBalanceAfter = entry.BalanceAfter
            });
        }

        _db.BusinessLedgerTransactions.Add(transaction);
        return transaction;
    }

    private BusinessLedgerTransaction PostIdempotentLedgerTransaction(
        BusinessWallet wallet,
        BusinessLedgerTransactionType type,
        decimal amount,
        string description,
        Guid? userId,
        string idempotencyKey,
        string requestHash,
        Guid? transferId,
        Guid? batchId,
        Guid? collectionId,
        params (BusinessLedgerAccountType Account, BusinessLedgerEntrySide Side, decimal? BalanceAfter)[] entries)
    {
        var transaction = PostLedgerTransaction(
            wallet,
            type,
            amount,
            description,
            userId,
            transferId,
            batchId,
            collectionId,
            entries);
        transaction.IdempotencyKey = idempotencyKey;
        transaction.IdempotencyRequestHash = requestHash;
        return transaction;
    }

    private async Task<BusinessLedgerTransaction?> FindIdempotentLedgerTransactionAsync(
        Guid businessProfileId,
        string idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        var existing = await _db.BusinessLedgerTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == businessProfileId &&
                x.IdempotencyKey == idempotencyKey &&
                !x.IsDeleted,
                ct);

        if (existing is not null &&
            !string.Equals(existing.IdempotencyRequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The supplied Idempotency-Key was already used for a different funding request.");
        }

        return existing;
    }

    private static string NormalizeIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("The Idempotency-Key header is required for this operation.");
        var cleaned = value.Trim();
        if (cleaned.Length > 200)
            throw new InvalidOperationException("The Idempotency-Key header cannot exceed 200 characters.");
        return cleaned;
    }

    private static string ComputeRequestHash(object request)
    {
        var json = JsonSerializer.Serialize(request);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static object Snapshot(BusinessWallet wallet) => new
    {
        wallet.Status,
        wallet.SettledBalance,
        wallet.AvailableBalance,
        wallet.HeldBalance
    };

    private static BusinessLedgerTransactionDto ToLedgerDto(BusinessLedgerTransaction x) =>
        new(
            x.Id,
            x.Reference,
            x.CurrencyCode,
            x.Type,
            x.Status,
            x.Amount,
            x.Description,
            x.TransferId,
            x.BusinessPaymentBatchId,
            x.CollectionId,
            x.PostedAt,
            x.Entries.OrderBy(y => y.CreatedAt)
                .Select(y => new BusinessLedgerEntryDto(
                    y.Id,
                    y.AccountType,
                    y.Side,
                    y.Amount,
                    y.AccountBalanceAfter))
                .ToList(),
            x.ReversalOfTransactionId,
            x.ReversedByTransactionId,
            x.ReversedAt,
            x.ReversalReason);

    private void ApplyWalletFundingTransition(
        Transfer transfer,
        string source,
        Guid? userId,
        string reservationReference)
    {
        _transferStatusService.ApplyTransition(
            transfer,
            TransferStatus.PaymentReceived,
            new TransferStatusTransitionContext(
                Source: source,
                Reason: "Business-wallet funds were reserved successfully.",
                ChangedByUserId: userId,
                EventType: "BUSINESS_WALLET_FUNDED",
                Title: "Transfer funded",
                Description: "Funds were reserved from the business wallet for this transfer.",
                MetadataJson: JsonSerializer.Serialize(new { reservationReference })));
    }

    private static void ValidateExternalCollectionTransfer(Transfer transfer)
    {
        if (!transfer.BusinessProfileId.HasValue)
            throw new InvalidOperationException("Only business transfers can use business external collections.");
        if (transfer.BusinessFundingSource != BusinessFundingSource.ExternalCollection)
            throw new InvalidOperationException("The transfer is not configured for external collection funding.");
        if (transfer.Status != TransferStatus.PendingPayment)
            throw new InvalidOperationException(
                $"External funding cannot be initiated while the transfer status is '{transfer.Status}'.");
        if (transfer.ApprovalStatus is BusinessApprovalStatus.Pending or BusinessApprovalStatus.Rejected)
            throw new InvalidOperationException("The transfer must complete approval before funding can be initiated.");
    }

    private static void EnsureWalletNotClosed(BusinessWallet wallet)
    {
        if (wallet.Status == BusinessWalletStatus.Closed)
            throw new InvalidOperationException("Business wallet is closed.");
    }

    private static void EnsureWalletActive(BusinessWallet wallet)
    {
        if (wallet.Status != BusinessWalletStatus.Active)
        {
            throw new InvalidOperationException($"Business wallet is '{wallet.Status}'.");
        }
    }

    private async Task<string> GenerateUniqueCollectionReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var reference = GenerateReference("KXBCOL");
            if (!await _db.Collections.AsNoTracking().AnyAsync(x => x.Reference == reference, ct))
                return reference;
        }
        throw new InvalidOperationException("Unable to generate a unique business collection reference.");
    }

    private string ResolveCollectionWalletId(string currencyCode)
    {
        if (!_collectionWalletIds.TryGetValue(currencyCode, out var walletId) || string.IsNullOrWhiteSpace(walletId))
        {
            throw new InvalidOperationException(
                $"Blaaiz collection wallet is not configured for {currencyCode}.");
        }
        return walletId;
    }

    private async Task UpsertProviderTransactionAsync(
        Collection collection,
        RemittanceCollectionResult result,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions.FirstOrDefaultAsync(x =>
            x.ProviderCode == _provider.ProviderCode &&
            x.ProviderTransactionId == result.ProviderTransactionId,
            ct);
        if (transaction is null)
        {
            transaction = new ProviderTransaction
            {
                ProviderCode = _provider.ProviderCode,
                TransferId = collection.TransferId,
                CollectionId = collection.Id,
                ProviderTransactionId = result.ProviderTransactionId,
                TransactionType = "collection"
            };
            _db.ProviderTransactions.Add(transaction);
        }

        transaction.ProviderReference = result.ProviderReference;
        transaction.ProviderStatus = result.ProviderStatus;
        transaction.CurrencyCode = collection.CurrencyCode;
        transaction.Amount = collection.Amount;
        transaction.RawPayloadJson = result.RawResponseJson;
        transaction.ProviderCreatedAt = null;
        transaction.LastSyncedAt = DateTime.UtcNow;
    }

    private static CollectionDetailsDto ToCollectionDetailsDto(Collection collection)
    {
        var transfer = collection.Transfer;
        return new CollectionDetailsDto(
            new CollectionDto
            {
                Id = collection.Id,
                TransferId = collection.TransferId,
                TransferReference = transfer.Reference,
                TransferStatus = transfer.Status,
                Reference = collection.Reference,
                SourceCountryCode = transfer.SourceCountryCode,
                CurrencyCode = collection.CurrencyCode,
                Amount = collection.Amount,
                PaymentMethod = collection.PaymentMethod,
                Status = collection.Status,
                ProviderCode = collection.ProviderCode,
                ProviderCollectionId = collection.ProviderCollectionId,
                ProviderReference = collection.ProviderReference,
                CheckoutUrl = collection.CheckoutUrl,
                ProviderExpiresAt = collection.ProviderExpiresAt,
                VirtualAccountNumber = collection.VirtualAccountNumber,
                VirtualAccountBankName = collection.VirtualAccountBankName,
                VirtualAccountName = collection.VirtualAccountName,
                InitiatedAt = collection.InitiatedAt,
                ConfirmedAt = collection.ConfirmedAt,
                FailedAt = collection.FailedAt,
                ExpiredAt = collection.ExpiredAt,
                RefundInitiatedAt = collection.RefundInitiatedAt,
                RefundedAt = collection.RefundedAt,
                ProviderRefundId = collection.ProviderRefundId,
                ProviderRefundReference = collection.ProviderRefundReference,
                RefundReason = collection.RefundReason,
                RefundFailureReason = collection.RefundFailureReason,
                LastRefundSyncedAt = collection.LastRefundSyncedAt,
                FailureReason = collection.FailureReason,
                CreatedAt = collection.CreatedAt,
                LastUpdatedAt = collection.LastUpdatedAt
            },
            collection.Attempts
                .OrderByDescending(x => x.AttemptedAt)
                .Select(x => new CollectionAttemptDto(
                    x.Id,
                    x.CollectionId,
                    x.Status,
                    x.ProviderRequestId,
                    x.ProviderResponseId,
                    x.RequestPayloadJson,
                    x.ResponsePayloadJson,
                    x.AttemptedAt,
                    x.ErrorMessage))
                .ToList());
    }

    private static BusinessWalletDto ToWalletDto(BusinessWallet wallet) =>
        new(
            wallet.Id,
            wallet.BusinessProfileId,
            wallet.CurrencyCode,
            wallet.Status,
            wallet.SettledBalance,
            wallet.AvailableBalance,
            wallet.HeldBalance,
            wallet.CreatedAt,
            wallet.LastUpdatedAt);

    private static string GenerateReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

    private static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Currency code is required.");
        return value.Trim().ToUpperInvariant();
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? "****" : $"**** **** **** {digits[^4..]}";
    }
}
