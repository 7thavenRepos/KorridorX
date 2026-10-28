using KorridorX.Models.Payments;
using KorridorX.Services.PaymentChannels;
using KorridorX.Providers.Remittance.Blaaiz;
using System.Data;
using KorridorX.Data;
using KorridorX.Dtos.Transfers;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Services.References;
using KorridorX.Services.Compliance;
using KorridorX.Services.Payments;
using KorridorX.Services.Recipients;
using KorridorX.Services.Security;
using KorridorX.Services.Wallets;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Transfers;

public class TransferService : ITransferService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ITransferStatusService _transferStatusService;
    private readonly IComplianceLimitService _complianceLimitService;
    private readonly ITransferRiskService _transferRiskService;
    private readonly IComplianceScreeningService _screeningService;
    private readonly ITransactionMonitoringService _transactionMonitoringService;
    private readonly IOutboundFundsRestrictionService _outboundFundsRestrictions;
    private readonly ITransactionPinService _transactionPinService;
    private readonly IConsumerTransferFundingService _consumerFunding;
    private readonly PaymentChannelService? _paymentChannels;

    public TransferService(
        AppDbContext db,
        IReferenceGenerator referenceGenerator,
        ITransferStatusService transferStatusService,
        IComplianceLimitService complianceLimitService,
        ITransferRiskService transferRiskService,
        IComplianceScreeningService screeningService,
        ITransactionMonitoringService transactionMonitoringService,
        IOutboundFundsRestrictionService outboundFundsRestrictions,
        ITransactionPinService transactionPinService,
        IConsumerTransferFundingService consumerFunding,
        PaymentChannelService? paymentChannels = null)
    {
        _db = db;
        _referenceGenerator = referenceGenerator;
        _transferStatusService = transferStatusService;
        _complianceLimitService = complianceLimitService;
        _transferRiskService = transferRiskService;
        _screeningService = screeningService;
        _transactionMonitoringService = transactionMonitoringService;
        _outboundFundsRestrictions = outboundFundsRestrictions;
        _transactionPinService = transactionPinService;
        _consumerFunding = consumerFunding;
        _paymentChannels = paymentChannels;
    }

    public async Task<TransferDetailsDto> CreateTransferAsync(
        Guid userId,
        CreateTransferRequestDto request,
        CancellationToken ct = default)
    {
        if (request.RecipientBankAccountId is not null && request.RecipientMobileWalletId is not null)
        {
            throw new InvalidOperationException("Select either a recipient bank account or mobile wallet, not both.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var customerProfile = await _db.CustomerProfiles
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                !x.IsDeleted,
                ct);

        if (customerProfile is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
        }

        await _outboundFundsRestrictions.EnsureUserOutboundAllowedAsync(
            userId,
            "consumer_transfer_create",
            ct);

        var quote = await _db.TransferQuotes
            .FirstOrDefaultAsync(x =>
                x.Id == request.TransferQuoteId &&
                x.CustomerProfileId == customerProfile.Id &&
                !x.IsDeleted,
                ct);

        if (quote is null)
        {
            throw new InvalidOperationException("Transfer quote not found.");
        }

        if (quote.IsUsed)
        {
            throw new InvalidOperationException("Transfer quote has already been used.");
        }

        if (quote.IsExpired)
        {
            throw new InvalidOperationException("Transfer quote has expired. Please create a new quote.");
        }

        if (quote.TransferType != TransferType.ConsumerToConsumer)
        {
            throw new InvalidOperationException("The selected quote is not a consumer transfer quote.");
        }

        var assetsStillEnabled = await _db.Assets.AsNoTracking()
            .CountAsync(x => x.IsSupported && x.IndividualEnabled &&
                (x.Code == quote.SourceCurrencyCode || x.Code == quote.DestinationCurrencyCode), ct);
        if (assetsStillEnabled != (quote.SourceCurrencyCode == quote.DestinationCurrencyCode ? 1 : 2))
            throw new InvalidOperationException("One or more quoted assets are no longer available to individual users.");

        var deliveryMode = RecipientDeliveryPolicy.Resolve(
            quote.DestinationCountryCode, quote.DestinationCurrencyCode);
        if (deliveryMode is null)
            throw new InvalidOperationException(
                "No supported recipient delivery method is configured for this destination.");

        PaymentMethod? requiredPayoutMethod = deliveryMode == RecipientDeliveryPolicy.EmailTransfer
            ? PaymentMethod.Interac : null;

        if (deliveryMode == RecipientDeliveryPolicy.EmailTransfer)
        {
            if (!string.Equals(quote.ProviderCode, "Blaaiz", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected payout provider does not support CAD email delivery.");
            if (request.RecipientBankAccountId.HasValue || request.RecipientMobileWalletId.HasValue)
                throw new InvalidOperationException("CAD Interac delivery uses the recipient email, not bank or mobile wallet details.");
        }
        else
        {
            if (!request.RecipientBankAccountId.HasValue)
                throw new InvalidOperationException("A ready recipient bank account is required for this payout.");
            PayoutDestinationCapabilityPolicy.EnsureSupported(
                quote.ProviderCode, PayoutDestinationType.RecipientBankAccount);
        }

        if (!string.Equals(quote.SourceCountryCode, customerProfile.CountryCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The quote source country no longer matches the customer profile.");
        }

        var recipient = await _db.Recipients
            .FirstOrDefaultAsync(x =>
                x.Id == request.RecipientId &&
                x.CustomerProfileId == customerProfile.Id &&
                x.IsActive &&
                !x.IsDeleted,
                ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        if (!string.Equals(recipient.CountryCode, quote.DestinationCountryCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The recipient country does not match the quote destination country.");
        }

        if (deliveryMode == RecipientDeliveryPolicy.EmailTransfer &&
            (string.IsNullOrWhiteSpace(recipient.Email) ||
             !System.Net.Mail.MailAddress.TryCreate(recipient.Email, out _)))
        {
            throw new InvalidOperationException("A valid recipient email is required for CAD Interac delivery.");
        }

        if (request.RecipientBankAccountId is not null)
        {
            var bankAccount = await _db.RecipientBankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.RecipientBankAccountId.Value &&
                    x.RecipientId == recipient.Id &&
                    x.CountryCode == recipient.CountryCode &&
                    x.CurrencyCode == quote.DestinationCurrencyCode &&
                    x.IsActive &&
                    !x.IsDeleted,
                    ct);

            if (bankAccount is null)
            {
                throw new InvalidOperationException("Recipient bank account is not valid for this transfer.");
            }

            requiredPayoutMethod = RecipientPayoutPolicy.Method(bankAccount);
            if (BlaaizCurrencyRequirements.IsForeignBank(bankAccount.CurrencyCode))
            {
                if (!string.Equals(quote.ProviderCode, "Blaaiz", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("This bank method requires Blaaiz.");
                RecipientPayoutPolicy.Validate(bankAccount);
            }
            else if (!await IsBankDestinationVerifiedForProviderAsync(
                    PayoutDestinationType.RecipientBankAccount,
                    bankAccount.Id,
                    quote.ProviderCode,
                    bankAccount.IsVerified,
                    ct))
            {
                throw new InvalidOperationException(
                    "Recipient bank account is not verified for the selected payout provider.");
            }
        }

        if (request.RecipientMobileWalletId is not null)
        {
            var mobileWalletExists = await _db.RecipientMobileWallets
                .AnyAsync(x =>
                    x.Id == request.RecipientMobileWalletId.Value &&
                    x.RecipientId == recipient.Id &&
                    x.CountryCode == recipient.CountryCode &&
                    x.CurrencyCode == quote.DestinationCurrencyCode &&
                    x.IsActive &&
                    !x.IsDeleted,
                    ct);

            if (!mobileWalletExists)
            {
                throw new InvalidOperationException("Recipient mobile wallet is not valid for this transfer.");
            }
        }

        // Reuse dispatch eligibility before creating a transfer or reserving
        // customer money, including managed CAD/NGN recipient routes.
        if (string.Equals(quote.ProviderCode, "Blaaiz", StringComparison.OrdinalIgnoreCase))
        {
            var foreignBank = BlaaizCurrencyRequirements.IsForeignBank(quote.DestinationCurrencyCode);
            if (_paymentChannels is null && foreignBank)
                throw new InvalidOperationException("Payout channel readiness is unavailable.");
            if (_paymentChannels is not null)
            {
                try
                {
                    await _paymentChannels.ResolvePayoutAsync(new Transfer
                    {
                        SourceCurrencyCode = quote.SourceCurrencyCode,
                        DestinationCurrencyCode = quote.DestinationCurrencyCode,
                        DestinationCountryCode = quote.DestinationCountryCode,
                        DestinationAmount = quote.DestinationAmount
                    }, null, ct, requiredPayoutMethod, requireManagedChannel: foreignBank);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        $"Recipient delivery from {quote.SourceCurrencyCode} to {quote.DestinationCurrencyCode} is currently unavailable. " +
                        "No wallet funds were reserved. Contact support with code KX-PAYOUT-005.", ex);
                }
            }
        }

        // Verify the PIN only after the quote and destination are valid, so malformed
        // requests cannot consume the user's failed-attempt budget.
        await _transactionPinService.VerifyForTransactionAsync(
            userId,
            request.TransactionPin,
            ct);

        var reference = await GenerateUniqueTransferReferenceAsync(ct);

        var transfer = new Transfer
        {
            Reference = reference,

            CustomerProfileId = customerProfile.Id,
            RecipientId = recipient.Id,
            RecipientBankAccountId = request.RecipientBankAccountId,
            RecipientMobileWalletId = request.RecipientMobileWalletId,
            TransferQuoteId = quote.Id,

            TransferType = TransferType.ConsumerToConsumer,
            Purpose = request.Purpose,
            PurposeNote = request.PurposeNote,

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
            Status = TransferStatus.Draft,

            CreatedByUserId = userId
        };

        quote.IsUsed = true;
        quote.UsedAt = DateTime.UtcNow;
        quote.LastUpdatedAt = DateTime.UtcNow;
        quote.LastUpdatedByUserId = userId;

        await _complianceLimitService.EnsureWithinLimitsAsync(transfer, ct);

        _db.Transfers.Add(transfer);

        _transferStatusService.ApplyTransition(
            transfer,
            TransferStatus.PendingPayment,
            new TransferStatusTransitionContext(
                Source: "Customer",
                Reason: "Transfer created from accepted quote.",
                ChangedByUserId: userId,
                EventType: "TRANSFER_CREATED",
                Title: "Transfer created",
                Description: "Your transfer has been created and is pending payment."));

        await _consumerFunding.PrepareTransferAsync(userId, transfer, ct);

        await _transferRiskService.AssessAsync(transfer, userId, ct);
        await _screeningService.ScreenTransferAsync(transfer, userId, ct);
        await _transactionMonitoringService.MonitorAsync(transfer, userId, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw new InvalidOperationException(
                "The transfer quote was consumed while this transfer was being created. Please create a new quote.");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();

            var quoteAlreadyConsumed = await _db.Transfers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.TransferQuoteId == quote.Id &&
                    !x.IsDeleted,
                    ct);

            if (quoteAlreadyConsumed)
            {
                throw new InvalidOperationException(
                    "The transfer quote has already been used to create a transfer. Please create a new quote.");
            }

            throw;
        }

        return await GetTransferByIdAsync(userId, transfer.Id, ct);
    }

    public async Task<TransferDetailsDto> GetTransferByIdAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var transfer = await _db.Transfers
            .AsNoTracking()
            .Include(x => x.CustomerProfile)
            .Include(x => x.StatusHistories)
            .Include(x => x.TimelineEvents)
            .FirstOrDefaultAsync(x =>
                x.Id == transferId &&
                x.CustomerProfileId != null &&
                x.CustomerProfile!.UserId == userId &&
                !x.IsDeleted,
                ct);

        if (transfer is null)
        {
            throw new InvalidOperationException("Transfer not found.");
        }

        return ToDetailsDto(transfer);
    }

    public async Task<PagedResult<TransferDto>> GetMyTransfersAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var customerProfileId = await _db.CustomerProfiles
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                !x.IsDeleted)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (customerProfileId is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
        }

        return await _db.Transfers
            .AsNoTracking()
            .Where(x =>
                x.CustomerProfileId == customerProfileId.Value &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TransferDto(
                x.Id,
                x.Reference,
                x.CustomerProfileId!.Value,
                x.RecipientId!.Value,
                x.RecipientBankAccountId,
                x.RecipientMobileWalletId,
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
                x.FeeCurrencyCode,
                x.TotalPayableAmount,
                x.CustomerRate,
                x.ProviderRate,
                x.Status,
                x.ProviderCode,
                x.ProviderTransferId,
                x.ProviderReference,
                x.PaymentReceivedAt,
                x.PayoutInitiatedAt,
                x.CompletedAt,
                x.FailedAt,
                x.CancelledAt,
                x.FailureReason,
                x.CreatedAt,
                x.LastUpdatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<TransferDetailsDto> CancelTransferAsync(
        Guid userId, Guid transferId, CancelTransferRequestDto request,
        CancellationToken ct = default)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Cancelled by customer before processing."
            : request.Reason.Trim();
        if (reason.Length > 1000)
            throw new InvalidOperationException("Cancellation reason cannot exceed 1000 characters.");

        // Serialize customer cancellation against the payout's durable dispatch
        // claim. No external payout may start once this state change commits.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var transfer = await _db.Transfers.FromSqlInterpolated(
                    $"SELECT * FROM \"Transfers\" WHERE \"Id\" = {transferId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (transfer is null || transfer.CustomerProfileId is not Guid profileId ||
                !await _db.CustomerProfiles.AsNoTracking().AnyAsync(x =>
                    x.Id == profileId && x.UserId == userId && !x.IsDeleted, ct))
                throw new InvalidOperationException("Transfer not found.");

            if (transfer.Status == TransferStatus.Cancelled)
            {
                await tx.CommitAsync(ct);
                return await GetTransferByIdAsync(userId, transferId, ct);
            }

            if (transfer.Status is not (TransferStatus.PendingPayment or TransferStatus.PaymentReceived) ||
                !string.IsNullOrWhiteSpace(transfer.ProviderTransferId) ||
                !string.IsNullOrWhiteSpace(transfer.ProviderReference) ||
                transfer.PayoutInitiatedAt.HasValue ||
                await _db.Payouts.AsNoTracking().AnyAsync(x => x.TransferId == transferId, ct))
                throw new InvalidOperationException(
                    "This transfer has entered processing or needs review and can no longer be cancelled online.");

            // Release only this consumer transfer's existing reservation. A
            // collection arriving after cancellation is credited to the wallet
            // by the existing collection-credit path, never silently discarded.
            await _consumerFunding.ReleaseActiveTransferReservationAsync(
                transfer, reason, userId, "CustomerCancellation", ct);

            _transferStatusService.ApplyTransition(
                transfer, TransferStatus.Cancelled,
                new TransferStatusTransitionContext(
                    Source: "Customer",
                    Reason: reason,
                    ChangedByUserId: userId,
                    EventType: "TRANSFER_CANCELLED",
                    Title: "Transfer cancelled",
                    Description: "The transfer was cancelled before processing. Any reserved wallet funds are available again."));

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetTransferByIdAsync(userId, transferId, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException(
                "The transfer status changed during cancellation. Refresh the transaction before trying again.");
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<string> GenerateUniqueTransferReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateTransferReference();

            var exists = await _db.Transfers
                .AsNoTracking()
                .AnyAsync(x => x.Reference == reference, ct);

            if (!exists)
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique transfer reference.");
    }

    private static TransferDetailsDto ToDetailsDto(Transfer transfer)
    {
        return new TransferDetailsDto(
            ToDto(transfer),
            transfer.StatusHistories
                .OrderByDescending(x => x.ChangedAt)
                .Select(ToStatusHistoryDto)
                .ToList(),
            TransferTimelinePresentation.Build(transfer.TimelineEvents, transfer.Status)
        );
    }

    private static TransferDto ToDto(Transfer transfer)
    {
        return new TransferDto(
            transfer.Id,
            transfer.Reference,
            transfer.CustomerProfileId!.Value,
            transfer.RecipientId!.Value,
            transfer.RecipientBankAccountId,
            transfer.RecipientMobileWalletId,
            transfer.TransferQuoteId,
            transfer.TransferType,
            transfer.Purpose,
            transfer.PurposeNote,
            transfer.SourceCountryCode,
            transfer.DestinationCountryCode,
            transfer.SourceCurrencyCode,
            transfer.DestinationCurrencyCode,
            transfer.SourceAmount,
            transfer.DestinationAmount,
            transfer.FeeAmount,
            transfer.FeeCurrencyCode,
            transfer.TotalPayableAmount,
            transfer.CustomerRate,
            transfer.ProviderRate,
            transfer.Status,
            transfer.ProviderCode,
            transfer.ProviderTransferId,
            transfer.ProviderReference,
            transfer.PaymentReceivedAt,
            transfer.PayoutInitiatedAt,
            transfer.CompletedAt,
            transfer.FailedAt,
            transfer.CancelledAt,
            transfer.FailureReason,
            transfer.CreatedAt,
            transfer.LastUpdatedAt
        );
    }

    private static TransferStatusHistoryDto ToStatusHistoryDto(TransferStatusHistory history)
    {
        return new TransferStatusHistoryDto(
            history.Id,
            history.TransferId,
            history.OldStatus,
            history.NewStatus,
            history.Reason,
            history.Source,
            history.ChangedByUserId,
            history.ChangedAt
        );
    }



    private async Task<bool> IsBankDestinationVerifiedForProviderAsync(
        PayoutDestinationType destinationType,
        Guid destinationId,
        string providerCode,
        bool legacyIsVerified,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ProviderCode>(providerCode, true, out var parsedProviderCode))
        {
            return legacyIsVerified;
        }

        var mapping = await _db.PayoutDestinationProviderMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.DestinationType == destinationType &&
                x.DestinationId == destinationId &&
                x.ProviderCode == parsedProviderCode &&
                !x.IsDeleted,
                ct);

        return mapping is null
            ? legacyIsVerified
            : mapping.IsActive && mapping.IsVerified;
    }

}
