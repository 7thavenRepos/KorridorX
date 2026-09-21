using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Payments;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Recipients;
using KorridorX.Models.Transfers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Compliance;
using KorridorX.Services.BusinessFunding;
using KorridorX.Services.References;
using KorridorX.Services.Transfers;
using KorridorX.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KorridorX.Services.Providers;

namespace KorridorX.Services.Payments;

public class PayoutService : IPayoutService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly IPayoutStatusService _payoutStatusService;
    private readonly IRemittanceProvider _remittanceProvider;
    private readonly IComplianceGateService _complianceGateService;
    private readonly ITransferStatusService _transferStatusService;
    private readonly IBusinessFundingService _businessFundingService;
    private readonly ITransferRiskService _transferRiskService;
    private readonly IComplianceScreeningService _screeningService;
    private readonly IOutboundFundsRestrictionService _outboundFundsRestrictions;
    private readonly IProviderWalletResolver _wallets;

    public PayoutService(
        AppDbContext db,
        IReferenceGenerator referenceGenerator,
        IPayoutStatusService payoutStatusService,
        IRemittanceProvider remittanceProvider,
        IComplianceGateService complianceGateService,
        ITransferStatusService transferStatusService,
        IBusinessFundingService businessFundingService,
        ITransferRiskService transferRiskService,
        IComplianceScreeningService screeningService,
        IOutboundFundsRestrictionService outboundFundsRestrictions,
        IProviderWalletResolver wallets)
    {
        _db = db;
        _referenceGenerator = referenceGenerator;
        _payoutStatusService = payoutStatusService;
        _remittanceProvider = remittanceProvider;
        _complianceGateService = complianceGateService;
        _transferStatusService = transferStatusService;
        _businessFundingService = businessFundingService;
        _transferRiskService = transferRiskService;
        _screeningService = screeningService;
        _outboundFundsRestrictions = outboundFundsRestrictions;
        _wallets = wallets;
    }

    public async Task<PayoutDetailsDto> DispatchForTransferAsync(
        Guid transferId,
        string source,
        Guid? changedByUserId = null,
        CancellationToken ct = default)
    {
        var transfer = await _db.Transfers
            .Include(x => x.CustomerProfile)
            .Include(x => x.BusinessProfile)
            .Include(x => x.Recipient)
            .Include(x => x.RecipientBankAccount)
            .Include(x => x.RecipientMobileWallet)
            .Include(x => x.BusinessBeneficiary)
            .Include(x => x.BusinessBeneficiaryBankAccount)
            .Include(x => x.BusinessBeneficiaryMobileWallet)
            .FirstOrDefaultAsync(x => x.Id == transferId && !x.IsDeleted, ct);

        if (transfer is null)
        {
            throw new InvalidOperationException("Transfer not found.");
        }

        var payout = await _db.Payouts
            .Include(x => x.Transfer)
            .ThenInclude(x => x!.CustomerProfile)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.TransferId == transferId && !x.IsDeleted, ct);

        if (payout is not null && payout.Status is PayoutStatus.Initiated or PayoutStatus.Processing or PayoutStatus.Successful)
        {
            return ToDetailsDto(payout);
        }

        if (transfer.Status is not TransferStatus.PaymentReceived and not TransferStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Payout cannot be dispatched while the transfer status is '{transfer.Status}'.");
        }

        if (transfer.IsOperationalHold)
        {
            throw new InvalidOperationException(
                $"Transfer payout is blocked by an operational hold: {transfer.OperationalHoldReason ?? "support review required"}.");
        }

        _transferRiskService.EnsureCanProceedToPayout(transfer);
        await _screeningService.EnsureTransferCanProceedToPayoutAsync(transfer, ct);
        await EnsureTransferOutboundRestrictionAsync(transfer, ct);

        var destination = await ResolveDestinationAsync(transfer, changedByUserId, ct);
        if (destination.IsMobileWallet)
        {
            throw new InvalidOperationException($"Mobile-wallet payouts are not yet enabled for {_remittanceProvider.ProviderName}.");
        }

        var paymentMethod = ResolvePayoutMethod(transfer.DestinationCurrencyCode);
        ValidateDestination(
            transfer.DestinationCurrencyCode,
            paymentMethod,
            destination.BankAccountIsActive,
            destination.BankAccountIsDeleted,
            destination.ProviderBankId,
            destination.AccountNumber,
            destination.BankAccountIsVerified,
            destination.VerifiedAccountName,
            destination.Email);

        var compliance = transfer.BusinessProfileId.HasValue
            ? await _complianceGateService.EnsureBusinessCanInitiateMoneyMovementAsync(
                transfer.BusinessProfileId.Value,
                _remittanceProvider.ProviderCode,
                ct)
            : await _complianceGateService.EnsureCanInitiateMoneyMovementAsync(
                transfer.CustomerProfileId
                    ?? throw new InvalidOperationException("Transfer customer profile is missing."),
                _remittanceProvider.ProviderCode,
                ct);

        var payoutProviderCustomerId = compliance.ProviderCustomerId;
        if (transfer.BusinessCustomerId.HasValue)
        {
            payoutProviderCustomerId = await _db.ProviderCustomers
                .AsNoTracking()
                .Where(x =>
                    x.ProviderCode == _remittanceProvider.ProviderCode &&
                    x.BusinessCustomerId == transfer.BusinessCustomerId.Value &&
                    !x.IsDeleted)
                .Select(x => x.ProviderCustomerId)
                .SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException(
                    "Embedded business customer has not completed provider onboarding for transfers.");
        }

        if (payout is null)
        {
            payout = new Payout
            {
                TransferId = transfer.Id,
                Transfer = transfer,
                Reference = await GenerateUniquePayoutReferenceAsync(ct),
                CurrencyCode = transfer.DestinationCurrencyCode,
                Amount = transfer.DestinationAmount,
                PaymentMethod = paymentMethod,
                Status = PayoutStatus.Pending,
                ProviderCode = transfer.ProviderCode,
                CreatedByUserId = changedByUserId
            };

            _db.Payouts.Add(payout);
            await _db.SaveChangesAsync(ct);
        }

        var walletId = await _wallets.SelectAsync("Payout", payout.Id, _remittanceProvider.ProviderName,
            transfer.SourceCurrencyCode, null, ProviderWalletResolver.Payout, payout.Attempts.Count > 0, ct);

        var sanitizedRequest = JsonSerializer.Serialize(new
        {
            providerWalletId = walletId,
            payoutId = payout.Id,
            payoutReference = payout.Reference,
            transferId = transfer.Id,
            transferReference = transfer.Reference,
            transferOwnerType = transfer.BusinessProfileId.HasValue ? "business" : "individual",
            sourceCurrency = transfer.SourceCurrencyCode,
            destinationCurrency = transfer.DestinationCurrencyCode,
            amount = transfer.DestinationAmount,
            paymentMethod = paymentMethod.ToString(),
            recipient = new
            {
                destination.FirstName,
                destination.LastName,
                destination.Email,
                bankName = destination.BankName,
                providerBankId = destination.ProviderBankId,
                providerPartyId = destination.ProviderPartyId,
                providerDestinationId = destination.ProviderDestinationId,
                accountNumber = MaskSensitive(destination.AccountNumber)
            }
        });

        var attempt = new PayoutAttempt
        {
            PayoutId = payout.Id,
            Payout = payout,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = sanitizedRequest,
            AttemptedAt = DateTime.UtcNow
        };

        payout.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        try
        {
            await EnsureTransferOutboundRestrictionAsync(transfer, ct);

            var result = await _remittanceProvider.InitiatePayoutAsync(
                new RemittancePayoutRequest(
                    transfer.Id,
                    payout.Id,
                    paymentMethod,
                    transfer.DestinationAmount,
                    transfer.SourceCurrencyCode,
                    transfer.DestinationCurrencyCode,
                    payoutProviderCustomerId,
                    walletId,
                    destination.FirstName,
                    destination.LastName,
                    destination.Email,
                    destination.PhoneNumber,
                    destination.ProviderBankId,
                    destination.BankName,
                    destination.VerifiedAccountName ?? destination.AccountName,
                    destination.AccountNumber,
                    destination.RoutingNumber,
                    destination.SortCode,
                    destination.Iban,
                    destination.SwiftBic,
                    $"KorridorX transfer {transfer.Reference}"),
                ct);

            var payoutStatus = MapProviderPayoutStatus(result.ProviderStatus);
            var metadata = JsonSerializer.Serialize(new
            {
                provider = _remittanceProvider.ProviderName,
                providerStatus = result.ProviderStatus,
                providerTransactionId = result.ProviderTransactionId,
                providerReference = result.ProviderReference,
                businessTransfer = transfer.BusinessProfileId.HasValue
            });

            _payoutStatusService.ApplyTransition(
                payout,
                payoutStatus,
                new PayoutStatusTransitionContext(
                    Source: source,
                    Reason: payoutStatus == PayoutStatus.Failed
                        ? $"{_remittanceProvider.ProviderName} rejected the payout."
                        : $"Payout submitted to {_remittanceProvider.ProviderName}.",
                    ChangedByUserId: changedByUserId,
                    ProviderPayoutId: result.ProviderTransactionId,
                    ProviderReference: result.ProviderReference,
                    InteracQuestion: result.InteracQuestion,
                    InteracAnswer: result.InteracAnswer,
                    ProviderRequestId: result.ProviderRequestLogId.ToString(),
                    ProviderResponseId: result.ProviderTransactionId,
                    RequestPayloadJson: sanitizedRequest,
                    ResponsePayloadJson: result.RawResponseJson,
                    MetadataJson: metadata,
                    OccurredAt: result.ProviderCreatedAt),
                attempt);

            await UpsertProviderTransactionAsync(
                payout,
                result.ProviderTransactionId,
                result.ProviderReference,
                result.ProviderStatus,
                result.CurrencyCode,
                result.Amount,
                result.RawResponseJson,
                result.ProviderCreatedAt,
                ct);

            transfer.ProviderTransferId = result.ProviderTransactionId;
            transfer.ProviderReference = result.ProviderReference;
            transfer.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDetailsDto(payout);
        }
        catch (ProviderIntegrationException ex)
        {
            _payoutStatusService.ApplyTransition(
                payout,
                PayoutStatus.Failed,
                new PayoutStatusTransitionContext(
                    Source: source,
                    Reason: ex.Message,
                    ChangedByUserId: changedByUserId,
                    ProviderRequestId: ex.RequestLogId?.ToString(),
                    RequestPayloadJson: sanitizedRequest,
                    ResponsePayloadJson: ex.ProviderResponse),
                attempt);

            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<PayoutDetailsDto> RetryFailedAsync(
        Guid payoutId,
        Guid changedByUserId,
        CancellationToken ct = default)
    {
        var payout = await _db.Payouts
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.Id == payoutId && !x.IsDeleted, ct);

        if (payout is null)
        {
            throw new InvalidOperationException("Payout not found.");
        }

        if (payout.Status != PayoutStatus.Failed)
        {
            throw new InvalidOperationException("Only a failed payout can be retried.");
        }

        var transfer = payout.Transfer
            ?? throw new InvalidOperationException("Remittance payout is missing its transfer.");

        if (transfer.Status != TransferStatus.RefundPending)
        {
            throw new InvalidOperationException(
                $"Payout retry requires the transfer to be in RefundPending, but it is '{transfer.Status}'.");
        }

        if (transfer.IsOperationalHold)
        {
            throw new InvalidOperationException(
                $"Payout retry is blocked by an operational hold: {transfer.OperationalHoldReason ?? "support review required"}.");
        }

        await _businessFundingService.ReactivateTransferReservationAsync(
            transfer,
            changedByUserId,
            "AdminRetry",
            ct);

        _transferStatusService.ApplyTransition(
            transfer,
            TransferStatus.Processing,
            new TransferStatusTransitionContext(
                Source: "Admin",
                Reason: "An operations user approved a retry of the failed recipient payout.",
                ChangedByUserId: changedByUserId,
                EventType: "PAYOUT_RETRY_APPROVED",
                Title: "Payout retry started",
                Description: "The failed recipient payout is being retried."));

        payout.ProviderPayoutId = null;
        payout.ProviderReference = null;
        payout.FailureReason = null;
        payout.FailedAt = null;
        payout.LastUpdatedAt = DateTime.UtcNow;
        payout.LastUpdatedByUserId = changedByUserId;
        transfer.ProviderTransferId = null;
        transfer.ProviderReference = null;

        await _db.SaveChangesAsync(ct);

        return await DispatchForTransferAsync(
            payout.TransferId ?? throw new InvalidOperationException("Remittance payout is missing its transfer."),
            "AdminRetry",
            changedByUserId,
            ct);
    }

    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 500);

        var transferIds = await _db.Transfers
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.Status == TransferStatus.PaymentReceived || x.Status == TransferStatus.Processing) &&
                !x.IsComplianceHold &&
                !x.IsOperationalHold &&
                !_db.Payouts.Any(p => p.TransferId == x.Id && !p.IsDeleted))
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        var dispatched = 0;
        foreach (var transferId in transferIds)
        {
            try
            {
                await DispatchForTransferAsync(transferId, "System", null, ct);
                dispatched++;
            }
            catch
            {
                // Each payout records its own failure state. The worker continues with the remaining transfers.
            }
        }

        return dispatched;
    }

    public async Task<PayoutDetailsDto> GetPayoutByIdAsync(
        Guid userId,
        Guid payoutId,
        CancellationToken ct = default)
    {
        var payout = await BuildOwnedPayoutQuery(userId)
            .FirstOrDefaultAsync(x => x.Id == payoutId, ct);

        if (payout is null)
        {
            throw new InvalidOperationException("Payout not found.");
        }

        return ToDetailsDto(payout);
    }

    public async Task<PayoutDetailsDto> GetTransferPayoutAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var payout = await BuildOwnedPayoutQuery(userId)
            .FirstOrDefaultAsync(x => x.TransferId == transferId, ct);

        if (payout is null)
        {
            throw new InvalidOperationException("No payout has been created for this transfer.");
        }

        return ToDetailsDto(payout);
    }

    public async Task<PagedResult<PayoutDto>> GetMyPayoutsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var paged = await _db.Payouts
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Where(x =>
                x.Purpose == PaymentOperationPurpose.Remittance &&
                x.TransferId != null &&
                x.Transfer != null &&
                x.Transfer.CustomerProfileId != null &&
                x.Transfer.CustomerProfile!.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile!.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<PayoutDto>
        {
            Items = paged.Items.Select(ToDto).ToList(),
            Meta = paged.Meta
        };
    }

    private IQueryable<Payout> BuildOwnedPayoutQuery(Guid userId) =>
        _db.Payouts
            .AsNoTracking()
            .Include(x => x.Transfer)
            .ThenInclude(x => x!.CustomerProfile)
            .Include(x => x.Attempts)
            .Where(x =>
                x.Purpose == PaymentOperationPurpose.Remittance &&
                x.TransferId != null &&
                x.Transfer != null &&
                x.Transfer.CustomerProfileId != null &&
                x.Transfer.CustomerProfile!.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile!.IsDeleted);

    private async Task UpsertProviderTransactionAsync(
        Payout payout,
        string providerTransactionId,
        string? providerReference,
        string providerStatus,
        string currencyCode,
        decimal amount,
        string rawPayload,
        DateTime? providerCreatedAt,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == _remittanceProvider.ProviderCode &&
                x.ProviderTransactionId == providerTransactionId,
                ct);

        if (transaction is null)
        {
            transaction = new ProviderTransaction
            {
                ProviderCode = _remittanceProvider.ProviderCode,
                TransferId = payout.TransferId,
                PayoutId = payout.Id,
                ProviderTransactionId = providerTransactionId,
                TransactionType = "payout"
            };
            _db.ProviderTransactions.Add(transaction);
        }

        transaction.TransferId = payout.TransferId;
        transaction.PayoutId = payout.Id;
        transaction.ProviderReference = providerReference;
        transaction.ProviderStatus = providerStatus;
        transaction.CurrencyCode = currencyCode;
        transaction.Amount = amount;
        transaction.RawPayloadJson = rawPayload;
        transaction.ProviderCreatedAt = providerCreatedAt;
        transaction.LastSyncedAt = DateTime.UtcNow;
        transaction.LastUpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> GenerateUniquePayoutReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GeneratePayoutReference();
            if (!await _db.Payouts.AsNoTracking().AnyAsync(x => x.Reference == reference, ct))
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique payout reference.");
    }

    private static PaymentMethod ResolvePayoutMethod(string destinationCurrencyCode) =>
        destinationCurrencyCode.ToUpperInvariant() switch
        {
            "NGN" => PaymentMethod.BankTransfer,
            "CAD" => PaymentMethod.Interac,
            _ => throw new InvalidOperationException(
                $"Automatic payout is not yet configured for {destinationCurrencyCode}.")
        };

    private async Task EnsureTransferOutboundRestrictionAsync(
        Transfer transfer,
        CancellationToken ct)
    {
        if (transfer.BusinessCustomerId.HasValue)
        {
            await _outboundFundsRestrictions
                .EnsureBusinessCustomerOutboundAllowedAsync(
                    transfer.BusinessCustomerId.Value,
                    "transfer_payout_dispatch",
                    ct);
            return;
        }

        if (transfer.BusinessProfileId.HasValue)
        {
            await _outboundFundsRestrictions
                .EnsureBusinessOutboundAllowedAsync(
                    transfer.BusinessProfileId.Value,
                    "transfer_payout_dispatch",
                    ct);
            return;
        }

        var userId = transfer.CustomerProfile?.UserId
            ?? throw new InvalidOperationException(
                "Transfer customer user is missing.");

        await _outboundFundsRestrictions
            .EnsureUserOutboundAllowedAsync(
                userId,
                "transfer_payout_dispatch",
                ct);
    }

    private async Task<PayoutDestination> ResolveDestinationAsync(
        Transfer transfer,
        Guid? changedByUserId,
        CancellationToken ct)
    {
        if (transfer.BusinessProfileId.HasValue)
        {
            var beneficiary = transfer.BusinessBeneficiary
                ?? throw new InvalidOperationException("Business transfer beneficiary is missing.");

            var names = SplitName(beneficiary.Name);
            var firstName = string.IsNullOrWhiteSpace(beneficiary.ContactFirstName)
                ? names.FirstName
                : beneficiary.ContactFirstName.Trim();
            var lastName = string.IsNullOrWhiteSpace(beneficiary.ContactLastName)
                ? names.LastName
                : beneficiary.ContactLastName.Trim();

            var account = transfer.BusinessBeneficiaryBankAccount;
            var wallet = transfer.BusinessBeneficiaryMobileWallet;

            var providerView = account is null
                ? null
                : await ResolveBankProviderViewAsync(
                    PayoutDestinationType.BusinessBeneficiaryBankAccount,
                    account.Id,
                    account.ProviderBankId,
                    account.ProviderBeneficiaryId,
                    account.ProviderBankAccountId,
                    account.IsVerified,
                    account.ProviderVerifiedAccountName,
                    account.ProviderVerificationReference,
                    account.VerificationAttemptedAt,
                    account.VerifiedAt,
                    account.LastVerificationError,
                    changedByUserId,
                    ct);

            return new PayoutDestination(
                firstName,
                lastName,
                beneficiary.Email,
                beneficiary.PhoneNumber,
                account?.BankName,
                providerView?.ProviderBankId ?? account?.ProviderBankId,
                providerView?.ProviderVerifiedAccountName ?? account?.ProviderVerifiedAccountName,
                account?.AccountName,
                account?.AccountNumber,
                account?.RoutingNumber,
                account?.SortCode,
                account?.Iban,
                account?.SwiftBic,
                account?.IsActive ?? false,
                account?.IsDeleted ?? false,
                providerView is not null
                ? providerView.IsActive && providerView.IsVerified
                : account?.IsVerified ?? false,
                wallet is not null,
                providerView?.ProviderPartyId ?? account?.ProviderBeneficiaryId,
                providerView?.ProviderDestinationId ?? account?.ProviderBankAccountId);
        }

        var recipient = transfer.Recipient
            ?? throw new InvalidOperationException("Transfer recipient is missing.");

        var recipientAccount = transfer.RecipientBankAccount;

        var recipientProviderView = recipientAccount is null
            ? null
            : await ResolveBankProviderViewAsync(
                PayoutDestinationType.RecipientBankAccount,
                recipientAccount.Id,
                recipientAccount.ProviderBankId,
                recipientAccount.ProviderRecipientId,
                recipientAccount.ProviderBankAccountId,
                recipientAccount.IsVerified,
                recipientAccount.ProviderVerifiedAccountName,
                recipientAccount.ProviderVerificationReference,
                recipientAccount.VerificationAttemptedAt,
                recipientAccount.VerifiedAt,
                recipientAccount.LastVerificationError,
                changedByUserId,
                ct);

        return new PayoutDestination(
            recipient.FirstName,
            recipient.LastName,
            recipient.Email,
            recipient.PhoneNumber,
            recipientAccount?.BankName,
            recipientProviderView?.ProviderBankId ?? recipientAccount?.ProviderBankId,
            recipientProviderView?.ProviderVerifiedAccountName ?? recipientAccount?.ProviderVerifiedAccountName,
            recipientAccount?.AccountName,
            recipientAccount?.AccountNumber,
            recipientAccount?.RoutingNumber,
            recipientAccount?.SortCode,
            recipientAccount?.Iban,
            recipientAccount?.SwiftBic,
            recipientAccount?.IsActive ?? false,
            recipientAccount?.IsDeleted ?? false,
            recipientProviderView is not null
                ? recipientProviderView.IsActive && recipientProviderView.IsVerified
                : recipientAccount?.IsVerified ?? false,
            transfer.RecipientMobileWallet is not null,
            recipientProviderView?.ProviderPartyId ?? recipientAccount?.ProviderRecipientId,
            recipientProviderView?.ProviderDestinationId ?? recipientAccount?.ProviderBankAccountId);
    }

    private async Task<PayoutDestinationProviderMapping?> ResolveBankProviderViewAsync(
        PayoutDestinationType destinationType,
        Guid destinationId,
        string? legacyProviderBankId,
        string? legacyProviderPartyId,
        string? legacyProviderDestinationId,
        bool legacyIsVerified,
        string? legacyVerifiedAccountName,
        string? legacyVerificationReference,
        DateTime? legacyVerificationAttemptedAt,
        DateTime? legacyVerifiedAt,
        string? legacyVerificationError,
        Guid? changedByUserId,
        CancellationToken ct)
    {
        var providerCode = _remittanceProvider.ProviderCode;

        var mapping = await _db.PayoutDestinationProviderMappings
            .FirstOrDefaultAsync(x =>
                x.DestinationType == destinationType &&
                x.DestinationId == destinationId &&
                x.ProviderCode == providerCode &&
                !x.IsDeleted,
                ct);

        var hasLegacyProviderState =
            !string.IsNullOrWhiteSpace(legacyProviderBankId) ||
            !string.IsNullOrWhiteSpace(legacyProviderPartyId) ||
            !string.IsNullOrWhiteSpace(legacyProviderDestinationId) ||
            legacyIsVerified ||
            !string.IsNullOrWhiteSpace(legacyVerifiedAccountName) ||
            !string.IsNullOrWhiteSpace(legacyVerificationReference);

        if (mapping is null)
        {
            if (!hasLegacyProviderState)
            {
                return null;
            }

            mapping = new PayoutDestinationProviderMapping
            {
                DestinationType = destinationType,
                DestinationId = destinationId,
                ProviderCode = providerCode,
                ProviderBankId = legacyProviderBankId,
                ProviderPartyId = legacyProviderPartyId,
                ProviderDestinationId = legacyProviderDestinationId,
                IsVerified = legacyIsVerified,
                VerificationAttemptedAt = legacyVerificationAttemptedAt,
                VerifiedAt = legacyVerifiedAt,
                ProviderVerifiedAccountName = legacyVerifiedAccountName,
                ProviderVerificationReference = legacyVerificationReference,
                LastVerificationError = legacyVerificationError,
                IsActive = true,
                CreatedByUserId = changedByUserId
            };

            _db.PayoutDestinationProviderMappings.Add(mapping);
            await _db.SaveChangesAsync(ct);
            return mapping;
        }

        var changed = false;

        if (string.IsNullOrWhiteSpace(mapping.ProviderBankId) &&
            !string.IsNullOrWhiteSpace(legacyProviderBankId))
        {
            mapping.ProviderBankId = legacyProviderBankId;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(mapping.ProviderPartyId) &&
            !string.IsNullOrWhiteSpace(legacyProviderPartyId))
        {
            mapping.ProviderPartyId = legacyProviderPartyId;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(mapping.ProviderDestinationId) &&
            !string.IsNullOrWhiteSpace(legacyProviderDestinationId))
        {
            mapping.ProviderDestinationId = legacyProviderDestinationId;
            changed = true;
        }

        if (changed)
        {
            mapping.LastUpdatedAt = DateTime.UtcNow;
            mapping.LastUpdatedByUserId = changedByUserId;
            await _db.SaveChangesAsync(ct);
        }

        return mapping;
    }

    private static void ValidateDestination(
        string currencyCode,
        PaymentMethod method,
        bool bankAccountIsActive,
        bool bankAccountIsDeleted,
        string? providerBankId,
        string? accountNumber,
        bool bankAccountIsVerified,
        string? verifiedAccountName,
        string? recipientEmail)
    {
        if (method == PaymentMethod.BankTransfer)
        {
            if (!bankAccountIsActive || bankAccountIsDeleted)
            {
                throw new InvalidOperationException("An active recipient bank account is required for payout.");
            }

            if (string.IsNullOrWhiteSpace(providerBankId) || string.IsNullOrWhiteSpace(accountNumber))
            {
                throw new InvalidOperationException(
                    $"A provider bank selection and account number are required for {currencyCode} payout.");
            }

            if (!bankAccountIsVerified || string.IsNullOrWhiteSpace(verifiedAccountName))
            {
                throw new InvalidOperationException(
                    "The recipient bank account must be verified with the provider before payout.");
            }
        }

        if (method == PaymentMethod.Interac && string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new InvalidOperationException("Recipient email is required for an Interac payout.");
        }
    }

    private static (string FirstName, string LastName) SplitName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return ("Business", "Beneficiary");
        if (parts.Length == 1) return (parts[0], "Beneficiary");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private sealed record PayoutDestination(
        string FirstName,
        string LastName,
        string? Email,
        string? PhoneNumber,
        string? BankName,
        string? ProviderBankId,
        string? VerifiedAccountName,
        string? AccountName,
        string? AccountNumber,
        string? RoutingNumber,
        string? SortCode,
        string? Iban,
        string? SwiftBic,
        bool BankAccountIsActive,
        bool BankAccountIsDeleted,
        bool BankAccountIsVerified,
        bool IsMobileWallet,
        string? ProviderPartyId,
        string? ProviderDestinationId);

    private static PayoutStatus MapProviderPayoutStatus(string status) =>
        status.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => PayoutStatus.Successful,
            "PROCESSING" => PayoutStatus.Processing,
            "FAILED" or "REJECTED" => PayoutStatus.Failed,
            "REVERSED" => PayoutStatus.Reversed,
            _ => PayoutStatus.Initiated
        };

    private static PayoutDetailsDto ToDetailsDto(Payout payout) =>
        new(
            ToDto(payout),
            payout.Attempts
                .OrderByDescending(x => x.AttemptedAt)
                .Select(x => new PayoutAttemptDto(
                    x.Id,
                    x.PayoutId,
                    x.Status,
                    x.ProviderRequestId,
                    x.ProviderResponseId,
                    x.RequestPayloadJson,
                    x.ResponsePayloadJson,
                    x.AttemptedAt,
                    x.ErrorMessage))
                .ToList());

    private static PayoutDto ToDto(Payout payout) =>
        new(
            payout.Id,
            payout.TransferId,
            payout.Purpose,
            payout.FinancialAccountId,
            payout.RelatedEntityType,
            payout.RelatedEntityId,
            payout.ContextEntityType,
            payout.ContextEntityId,
            payout.Transfer?.Reference,
            payout.Transfer?.Status,
            payout.Reference,
            payout.CurrencyCode,
            payout.Amount,
            payout.PaymentMethod,
            payout.Status,
            payout.ProviderCode,
            payout.ProviderPayoutId,
            payout.ProviderReference,
            payout.InteracQuestion,
            payout.InteracAnswer,
            payout.InitiatedAt,
            payout.CompletedAt,
            payout.FailedAt,
            payout.ReversedAt,
            payout.FailureReason,
            payout.CreatedAt,
            payout.LastUpdatedAt);

    private static string? MaskSensitive(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= 4 ? "****" : $"***{value[^4..]}";
    }
}
