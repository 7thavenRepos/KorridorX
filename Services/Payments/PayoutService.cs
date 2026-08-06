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
using KorridorX.Services.References;
using KorridorX.Services.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Payments;

public class PayoutService : IPayoutService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly IPayoutStatusService _payoutStatusService;
    private readonly IRemittanceProvider _remittanceProvider;
    private readonly IComplianceGateService _complianceGateService;
    private readonly ITransferStatusService _transferStatusService;
    private readonly IReadOnlyDictionary<string, string> _payoutWalletIds;

    public PayoutService(
        AppDbContext db,
        IReferenceGenerator referenceGenerator,
        IPayoutStatusService payoutStatusService,
        IRemittanceProvider remittanceProvider,
        IComplianceGateService complianceGateService,
        ITransferStatusService transferStatusService,
        IOptions<BlaaizOptions> blaaizOptions)
    {
        _db = db;
        _referenceGenerator = referenceGenerator;
        _payoutStatusService = payoutStatusService;
        _remittanceProvider = remittanceProvider;
        _complianceGateService = complianceGateService;
        _transferStatusService = transferStatusService;
        _payoutWalletIds = blaaizOptions.Value.PayoutWalletIds;
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
            .ThenInclude(x => x.CustomerProfile)
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

        var destination = ResolveDestination(transfer);
        if (destination.IsMobileWallet)
        {
            throw new InvalidOperationException("Blaaiz mobile-wallet payouts are not yet enabled.");
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

        var walletId = ResolvePayoutWalletId(transfer.SourceCurrencyCode);

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

        var sanitizedRequest = JsonSerializer.Serialize(new
        {
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
            var result = await _remittanceProvider.InitiatePayoutAsync(
                new RemittancePayoutRequest(
                    transfer.Id,
                    payout.Id,
                    paymentMethod,
                    transfer.DestinationAmount,
                    transfer.SourceCurrencyCode,
                    transfer.DestinationCurrencyCode,
                    compliance.ProviderCustomerId,
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
                    Reason: payoutStatus == PayoutStatus.Failed ? "Blaaiz rejected the payout." : "Payout submitted to Blaaiz.",
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

        if (payout.Transfer.Status != TransferStatus.RefundPending)
        {
            throw new InvalidOperationException(
                $"Payout retry requires the transfer to be in RefundPending, but it is '{payout.Transfer.Status}'.");
        }

        _transferStatusService.ApplyTransition(
            payout.Transfer,
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
        payout.Transfer.ProviderTransferId = null;
        payout.Transfer.ProviderReference = null;

        await _db.SaveChangesAsync(ct);

        return await DispatchForTransferAsync(
            payout.TransferId,
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
                x.Transfer.CustomerProfileId != null &&
                x.Transfer.CustomerProfile!.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile.IsDeleted)
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
            .ThenInclude(x => x.CustomerProfile)
            .Include(x => x.Attempts)
            .Where(x =>
                x.Transfer.CustomerProfileId != null &&
                x.Transfer.CustomerProfile!.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile.IsDeleted);

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

    private string ResolvePayoutWalletId(string currencyCode)
    {
        if (!_payoutWalletIds.TryGetValue(currencyCode, out var walletId) || string.IsNullOrWhiteSpace(walletId))
        {
            throw new InvalidOperationException($"No Blaaiz payout wallet is configured for {currencyCode}.");
        }

        return walletId.Trim();
    }

    private static PaymentMethod ResolvePayoutMethod(string destinationCurrencyCode) =>
        destinationCurrencyCode.ToUpperInvariant() switch
        {
            "NGN" => PaymentMethod.BankTransfer,
            "CAD" => PaymentMethod.Interac,
            _ => throw new InvalidOperationException(
                $"Automatic payout is not yet configured for {destinationCurrencyCode}.")
        };

    private static PayoutDestination ResolveDestination(Transfer transfer)
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

            return new PayoutDestination(
                firstName,
                lastName,
                beneficiary.Email,
                beneficiary.PhoneNumber,
                account?.BankName,
                account?.ProviderBankId,
                account?.ProviderVerifiedAccountName,
                account?.AccountName,
                account?.AccountNumber,
                account?.RoutingNumber,
                account?.SortCode,
                account?.Iban,
                account?.SwiftBic,
                account?.IsActive ?? false,
                account?.IsDeleted ?? false,
                account?.IsVerified ?? false,
                wallet is not null);
        }

        var recipient = transfer.Recipient
            ?? throw new InvalidOperationException("Transfer recipient is missing.");
        var recipientAccount = transfer.RecipientBankAccount;

        return new PayoutDestination(
            recipient.FirstName,
            recipient.LastName,
            recipient.Email,
            recipient.PhoneNumber,
            recipientAccount?.BankName,
            recipientAccount?.ProviderBankId,
            recipientAccount?.ProviderVerifiedAccountName,
            recipientAccount?.AccountName,
            recipientAccount?.AccountNumber,
            recipientAccount?.RoutingNumber,
            recipientAccount?.SortCode,
            recipientAccount?.Iban,
            recipientAccount?.SwiftBic,
            recipientAccount?.IsActive ?? false,
            recipientAccount?.IsDeleted ?? false,
            recipientAccount?.IsVerified ?? false,
            transfer.RecipientMobileWallet is not null);
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
        bool IsMobileWallet);

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
            payout.Transfer.Reference,
            payout.Transfer.Status,
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
