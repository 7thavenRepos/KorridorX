using KorridorX.Data;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Compliance;
using KorridorX.Services.PaymentChannels;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.WalletWithdrawals;

// W2A is read-only with respect to financial state. No payout, reservation,
// payment channel selection record, transfer intent or ledger posting is created.
public sealed class WalletWithdrawalFeePreviewService(
    AppDbContext db,
    WalletFundingContextService contexts,
    PaymentChannelService channels,
    IComplianceLimitService complianceLimits,
    IOutboundFundsRestrictionService outboundRestrictions,
    IBlaaizApiClient blaaiz) : IWalletWithdrawalFeePreviewService
{
    public async Task<WalletWithdrawalFeePreviewDto> PreviewAsync(Guid userId, Guid walletId, bool business,
        WalletWithdrawalFeePreviewRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DestinationId == Guid.Empty || request.ChannelId == Guid.Empty)
            throw new InvalidOperationException("Choose a saved withdrawal destination and an available payout channel.");

        // write:true enforces ManageFunding for business actors; LoadAsync does
        // not itself mutate balances or database entities.
        var context = await contexts.LoadAsync(userId, walletId, business, write: true,
            requireDeposits: false, ct);
        var wallet = context.Wallet;
        if (wallet.Status != FinancialAccountStatus.Active || wallet.Asset.Type != AssetType.Fiat ||
            !wallet.Asset.IsSupported || !wallet.Asset.WithdrawalEnabled ||
            (business ? !wallet.Asset.BusinessEnabled : !wallet.Asset.IndividualEnabled) ||
            !await db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == context.CountryCode &&
                x.AssetCode == wallet.AssetCode && x.Country.IsSupported && x.Asset.IsSupported &&
                x.CanWithdraw, ct))
            throw new InvalidOperationException("Withdrawal is not enabled for this wallet and customer profile.");

        WalletWithdrawalFeePolicy.ValidateAmount(request.WithdrawalAmount, wallet.AvailableBalance,
            wallet.Asset.DecimalPlaces);
        await outboundRestrictions.EnsureFinancialAccountOwnerOutboundAllowedAsync(
            wallet.OwnerType, context.OwnerId, "FiatWalletWithdrawalFeePreview", ct);
        var verifiedCustomer = await contexts.EnsureComplianceAsync(context, ct);
        if (!Guid.TryParse(verifiedCustomer.ProviderCustomerId, out var providerId) || providerId == Guid.Empty)
            throw new InvalidOperationException("A valid verified Blaaiz customer is required to quote a withdrawal.");

        var destination = await db.FiatWalletWithdrawalDestinations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.DestinationId && x.FinancialAccountId == walletId &&
                x.OwnerId == context.OwnerId && x.OwnerType == wallet.OwnerType && !x.IsDeleted && x.IsActive, ct)
            ?? throw new InvalidOperationException("This withdrawal destination is inactive or does not belong to the selected wallet.");

        var country = WalletWithdrawalSetupPolicy.DestinationCountry(wallet.AssetCode, destination.PaymentMethod);
        if (destination.AssetCode != wallet.AssetCode || destination.DestinationCountryCode != country ||
            destination.VerifiedAtUtc == default || string.IsNullOrWhiteSpace(destination.VerificationReference))
            throw new InvalidOperationException("The withdrawal destination requires fresh verification.");
        if (destination.PaymentMethod == PaymentMethod.BankTransfer &&
            (string.IsNullOrWhiteSpace(destination.ProviderBankId) ||
             string.IsNullOrWhiteSpace(destination.ProtectedAccountNumber) ||
             string.IsNullOrWhiteSpace(destination.AccountNumberLast4) ||
             string.IsNullOrWhiteSpace(destination.VerifiedAccountName)))
            throw new InvalidOperationException("The saved bank destination is incomplete and must be verified again.");
        if (destination.PaymentMethod == PaymentMethod.Interac)
        {
            var ownerUserId = business
                ? await db.BusinessProfiles.AsNoTracking().Where(x => x.Id == context.OwnerId)
                    .Select(x => x.OwnerUserId).SingleAsync(ct)
                : userId;
            var email = await db.Users.AsNoTracking().Where(x => x.Id == ownerUserId && x.EmailConfirmed)
                .Select(x => x.Email).SingleOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(email) ||
                !string.Equals(email.Trim(), destination.Email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The saved Interac address is no longer the owner's confirmed email.");
        }

        var eligible = await channels.EligibleAsync(country, wallet.AssetCode, business,
            PaymentChannelDirection.Withdrawal, wallet.AssetCode, request.WithdrawalAmount, ct);
        var matching = eligible.Where(x => x.Channel.Id == request.ChannelId &&
            x.Channel.AssetCode == wallet.AssetCode && x.Wallet.AssetCode == wallet.AssetCode &&
            x.Capability.PaymentMethod == destination.PaymentMethod).ToArray();
        if (matching.Length != 1)
            throw new InvalidOperationException("The payout channel is not available for this wallet, destination and amount.");

        // Reuse the existing compliance limit engine without creating a Transfer
        // row or compliance-check record. It includes current Send daily/monthly
        // exposure; W2B will add persisted withdrawal exposure at initiation.
        var limits = await complianceLimits.EvaluateAsync(new Transfer
        {
            CustomerProfileId = context.CustomerProfileId,
            BusinessProfileId = business ? context.OwnerId : null,
            SourceCountryCode = context.CountryCode,
            SourceCurrencyCode = wallet.AssetCode,
            TotalPayableAmount = request.WithdrawalAmount
        }, ct);
        if (!limits.IsAllowed)
            throw new ComplianceLimitExceededException(limits.FailureCode ?? "COMPLIANCE_LIMIT_EXCEEDED",
                limits.FailureMessage ?? "The withdrawal exceeds a configured compliance limit.");

        var raw = await blaaiz.GetFeeBreakdownAsync(new BlaaizFeeBreakdownRequest
        {
            FromCurrencyId = wallet.AssetCode,
            ToCurrencyId = wallet.AssetCode,
            FromAmount = request.WithdrawalAmount,
            CustomerId = providerId.ToString("D")
        }, ct);
        var values = WalletWithdrawalFeePolicy.Evaluate(raw.Data, destination.PaymentMethod,
            request.WithdrawalAmount, wallet.Asset.DecimalPlaces);

        // There is currently no fiat-withdrawal-specific KorridorX fee rule.
        // TransferFee belongs to Send/Trading; do not silently apply it here.
        // A dedicated Admin fee rule and exact W2B collection path are required
        // before ever charging a KorridorX withdrawal fee.
        const decimal korridorXFee = 0m;
        return new WalletWithdrawalFeePreviewDto(wallet.Id, matching[0].Channel.Id, destination.Id,
            wallet.AssetCode, wallet.Asset.DecimalPlaces, request.WithdrawalAmount,
            values.ProviderFee, korridorXFee, "NOT_CONFIGURED",
            request.WithdrawalAmount, values.RecipientAmount, wallet.AvailableBalance,
            new WalletWithdrawalDestinationDto(destination.Id, walletId, destination.AssetCode,
                destination.DestinationCountryCode, destination.PaymentMethod,
                destination.PaymentMethod == PaymentMethod.Interac ? "Interac e-Transfer" : "Bank transfer",
                destination.BankName,
                destination.AccountNumberLast4 is null ? null : "•••• " + destination.AccountNumberLast4,
                destination.VerifiedAccountName, destination.Email, destination.IsActive,
                destination.VerifiedAtUtc), DateTime.UtcNow, RequiresFreshQuoteBeforeExecution: true);
    }
}
