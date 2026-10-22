using System.Data;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Services.Audit;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.WalletWithdrawals;

// W2B1 persists only a held-balance withdrawal intent. No provider dispatch,
// capture, refund, retry or payout row is created by this service. The write
// endpoint is hard-locked to Development and separately disabled by default.
public sealed class WalletWithdrawalInitiationService(
    AppDbContext db,
    WalletFundingContextService contexts,
    IWalletWithdrawalFeePreviewService previews,
    IFinancialReservationService reservations,
    ITransactionPinService pins,
    IBusinessAccessService businessAccess,
    IComplianceLimitService complianceLimits,
    INotificationQueueService notifications,
    IAuditService audit,
    IOptions<WalletWithdrawalProcessingOptions> options,
    IHostEnvironment environment) : IWalletWithdrawalInitiationService
{
    public async Task<WalletWithdrawalDetailsDto> InitiateAsync(Guid userId, Guid walletId, bool business,
        InitiateWalletWithdrawalRequestDto request, string? idempotencyKey, CancellationToken ct = default)
    {
        // A guard cannot be bypassed by a staging/prod setting; W2B2 must
        // explicitly change this code after the payout lifecycle is verified.
        if (!environment.IsDevelopment() || !options.Value.Enabled)
            throw new InvalidOperationException("Fiat wallet withdrawal processing is not enabled. No funds have been reserved.");
        ArgumentNullException.ThrowIfNull(request);
        var keyHash = WalletWithdrawalInitiationPolicy.HashIdempotencyKey(idempotencyKey);
        var fingerprint = WalletWithdrawalInitiationPolicy.Fingerprint(walletId, request.DestinationId,
            request.ChannelId, request.WithdrawalAmount, business);

        var context = await contexts.LoadAsync(userId, walletId, business, write: true,
            requireDeposits: false, ct);
        var previouslyCreated = await FindIdempotentAsync(context, keyHash, fingerprint, ct);
        if (previouslyCreated is not null) return ToDto(previouslyCreated);

        if (business)
        {
            var access = await businessAccess.GetAccessAsync(userId, ct);
            if (access.BusinessProfileId != context.OwnerId)
                throw new UnauthorizedAccessException("The selected business does not own this wallet.");
            // W2B3 introduces a real approval lifecycle. A profile requiring
            // approval cannot bypass its policy via the new Withdrawal API.
            if (access.RequiresTransferApproval &&
                (!access.TransferApprovalThreshold.HasValue ||
                 request.WithdrawalAmount >= access.TransferApprovalThreshold.Value))
                throw new InvalidOperationException("Business withdrawal approval is required and is not yet enabled. No funds have been reserved.");
        }
        else
        {
            await pins.VerifyForTransactionAsync(userId, request.TransactionPin, ct);
        }

        // Always get a fresh *inclusive from_amount* provider quote. W2A
        // checks destination, permissions, KYC/KYB, channel, restrictions,
        // precision, balance and the existing compliance limit engine.
        var feeRequest = new WalletWithdrawalFeePreviewRequestDto
        {
            DestinationId = request.DestinationId,
            ChannelId = request.ChannelId,
            WithdrawalAmount = request.WithdrawalAmount
        };
        var quote = await previews.PreviewAsync(userId, walletId, business, feeRequest, ct);
        WalletWithdrawalInitiationPolicy.EnsureFreshQuote(quote, request.WithdrawalAmount, DateTime.UtcNow);

        // READ COMMITTED is intentional: a second request waiting on the
        // idempotency advisory lock must see the first request's committed
        // row after the lock is released. Wallet/exposure locks serialize all
        // balance and per-owner compliance mutations performed by this API.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // Unique across all wallets owned by this customer/business.
            // Serialize same-key submissions before any financial mutation.
            var lockKey = $"FiatWalletWithdrawal:{context.Wallet.OwnerType}:{context.OwnerId:N}:{keyHash}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);

            previouslyCreated = await FindIdempotentAsync(context, keyHash, fingerprint, ct);
            if (previouslyCreated is not null)
            {
                await tx.CommitAsync(ct);
                return ToDto(previouslyCreated);
            }

            // Recheck current wallet row while locked; the preview ran before
            // this serializable transaction and its balance may be stale.
            var lockedWallet = await db.FinancialAccounts.FromSqlInterpolated(
                $"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {walletId} FOR UPDATE")
                .SingleAsync(ct);
            await db.Entry(lockedWallet).ReloadAsync(ct);
            if (lockedWallet.OwnerType != context.Wallet.OwnerType || lockedWallet.OwnerId != context.OwnerId ||
                lockedWallet.Status != FinancialAccountStatus.Active ||
                lockedWallet.AssetCode != quote.WalletCurrency)
                throw new InvalidOperationException("The wallet became ineligible before funds could be reserved.");
            WalletWithdrawalFeePolicy.ValidateAmount(quote.TotalWalletDebit,
                lockedWallet.AvailableBalance, quote.CurrencyDecimalPlaces);

            var destination = await db.FiatWalletWithdrawalDestinations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.DestinationId &&
                    x.FinancialAccountId == walletId && x.OwnerId == context.OwnerId &&
                    x.OwnerType == lockedWallet.OwnerType && x.IsActive && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("The verified withdrawal destination is no longer active.");
            if (destination.VerifiedAtUtc == default || destination.PaymentMethod != quote.Destination.PaymentMethod ||
                destination.AssetCode != quote.WalletCurrency || string.IsNullOrWhiteSpace(destination.VerificationReference))
                throw new InvalidOperationException("The withdrawal destination must be verified again.");

            // Recheck combined transfer/withdrawal exposure inside the locked
            // transaction. Cross-wallet withdrawals share the owner key gate.
            var exposureKey = $"FiatWithdrawalExposure:{context.Wallet.OwnerType}:{context.OwnerId:N}:{context.CountryCode}:{quote.WalletCurrency}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({exposureKey}, 0))", ct);
            var limit = await complianceLimits.EvaluateAsync(new Transfer
            {
                CustomerProfileId = context.CustomerProfileId,
                BusinessProfileId = business ? context.OwnerId : null,
                SourceCountryCode = context.CountryCode,
                SourceCurrencyCode = quote.WalletCurrency,
                TotalPayableAmount = quote.TotalWalletDebit
            }, ct);
            if (!limit.IsAllowed)
                throw new ComplianceLimitExceededException(limit.FailureCode ?? "COMPLIANCE_LIMIT_EXCEEDED",
                    limit.FailureMessage ?? "The withdrawal exceeds a compliance limit.");

            // Fail closed when lock contention made the quoted price stale.
            WalletWithdrawalInitiationPolicy.EnsureFreshQuote(quote, request.WithdrawalAmount, DateTime.UtcNow);

            var row = new FiatWalletWithdrawal
            {
                FinancialAccountId = walletId,
                OwnerType = lockedWallet.OwnerType,
                OwnerId = context.OwnerId,
                CustomerProfileId = context.CustomerProfileId,
                BusinessProfileId = business ? context.OwnerId : null,
                RequestedByUserId = userId,
                CreatedByUserId = userId,
                SourceCountryCode = context.CountryCode,
                CurrencyCode = quote.WalletCurrency,
                CurrencyDecimalPlaces = quote.CurrencyDecimalPlaces,
                DestinationId = destination.Id,
                PaymentChannelId = quote.ChannelId,
                PaymentMethod = destination.PaymentMethod,
                BankName = destination.BankName,
                ProviderBankId = destination.ProviderBankId,
                ProtectedAccountNumber = destination.ProtectedAccountNumber,
                AccountNumberLast4 = destination.AccountNumberLast4,
                VerifiedAccountName = destination.VerifiedAccountName,
                RecipientEmail = destination.Email,
                Reference = "KXWD-" + Guid.NewGuid().ToString("N"),
                IdempotencyKeyHash = keyHash,
                RequestFingerprint = fingerprint,
                WithdrawalAmount = quote.WithdrawalAmount,
                ProviderFee = quote.ProviderFee,
                KorridorXFee = quote.KorridorXFee,
                KorridorXFeePolicy = quote.KorridorXFeePolicy,
                ExpectedRecipientAmount = quote.ExpectedRecipientAmount,
                TotalWalletDebit = quote.TotalWalletDebit,
                FeeQuotedAtUtc = quote.QuotedAtUtc,
                Status = FiatWalletWithdrawalStatus.AwaitingExecution
            };
            db.FiatWalletWithdrawals.Add(row);
            var reservation = await reservations.ReserveAsync(walletId, FinancialReservationType.Withdrawal,
                nameof(FiatWalletWithdrawal), row.Id, quote.TotalWalletDebit, userId,
                business ? "BusinessProfile" : "CustomerProfile",
                business ? context.OwnerId : context.CustomerProfileId, ct);
            row.FinancialReservationId = reservation.Id;

            audit.Stage(new AuditRecordRequest("FIAT_WALLET_WITHDRAWAL_RESERVED", "WalletWithdrawal",
                nameof(FiatWalletWithdrawal), row.Id.ToString(), NewValues: new
                {
                    row.Reference, row.CurrencyCode, row.TotalWalletDebit, row.ProviderFee,
                    row.ExpectedRecipientAmount, row.Status, row.PaymentMethod,
                    row.FinancialAccountId, row.DestinationId
                }, UserId: userId));
            var subject = $"Withdrawal request reserved: {row.CurrencyCode}";
            var message = "<p>Your withdrawal request was recorded and its amount reserved in your wallet.</p>" +
                "<p>No bank or Interac payout has been dispatched. The request is awaiting processing.</p>";
            if (business)
                await notifications.QueueBusinessAsync(context.OwnerId, subject, message,
                    nameof(FiatWalletWithdrawal), row.Id, ct);
            else
                await notifications.QueueUserAsync(userId, subject, message,
                    nameof(FiatWalletWithdrawal), row.Id, ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToDto(row);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<WalletWithdrawalDetailsDto> GetAsync(Guid userId, Guid walletId, Guid withdrawalId,
        bool business, CancellationToken ct = default)
    {
        var context = await contexts.LoadAsync(userId, walletId, business, write: false,
            requireDeposits: false, ct);
        var row = await db.FiatWalletWithdrawals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == withdrawalId && x.FinancialAccountId == walletId &&
            x.OwnerType == context.Wallet.OwnerType && x.OwnerId == context.OwnerId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Withdrawal not found for this wallet.");
        return ToDto(row);
    }

    public async Task<PagedResult<WalletWithdrawalDetailsDto>> ListAsync(Guid userId, Guid walletId,
        bool business, int page, int pageSize, CancellationToken ct = default)
    {
        var context = await contexts.LoadAsync(userId, walletId, business, write: false,
            requireDeposits: false, ct);
        var pageData = await db.FiatWalletWithdrawals.AsNoTracking().Where(x =>
                x.FinancialAccountId == walletId && x.OwnerType == context.Wallet.OwnerType &&
                x.OwnerId == context.OwnerId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).PaginateAsync(page, pageSize, ct);
        return new PagedResult<WalletWithdrawalDetailsDto>
        {
            Items = pageData.Items.Select(ToDto).ToList(),
            Meta = pageData.Meta
        };
    }

    private async Task<FiatWalletWithdrawal?> FindIdempotentAsync(
        WalletFundingContext context, string keyHash, string fingerprint, CancellationToken ct)
    {
        var row = await db.FiatWalletWithdrawals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OwnerType == context.Wallet.OwnerType && x.OwnerId == context.OwnerId &&
            x.IdempotencyKeyHash == keyHash && !x.IsDeleted, ct);
        if (row is not null) WalletWithdrawalInitiationPolicy.EnsureSame(row, fingerprint);
        return row;
    }

    private static WalletWithdrawalDetailsDto ToDto(FiatWalletWithdrawal row) => new(
        row.Id, row.FinancialAccountId, row.DestinationId, row.PaymentChannelId,
        row.Reference, row.CurrencyCode, row.WithdrawalAmount, row.ProviderFee,
        row.KorridorXFee, row.KorridorXFeePolicy, row.TotalWalletDebit,
        row.ExpectedRecipientAmount, row.Status, row.PaymentMethod,
        row.BankName,
        row.AccountNumberLast4 is null ? null : "•••• " + row.AccountNumberLast4,
        row.RecipientEmail, row.FeeQuotedAtUtc, row.CreatedAt,
        ProviderDispatchEnabled: false);
}
