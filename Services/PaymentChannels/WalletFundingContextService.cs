using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Compliance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.PaymentChannels;

public sealed record WalletFundingContext(FinancialAccount Wallet, string CountryCode, bool Business,
    Guid OwnerId, Guid? CustomerProfileId, string? Email, string Name, string? Phone);

public sealed record VerifiedBusinessPrincipal(Guid OwnerUserId, Guid CustomerProfileId,
    ComplianceGateResult IndividualCompliance);

public sealed class WalletFundingContextService(AppDbContext db, IBusinessAccessService businessAccess,
    IComplianceGateService compliance)
{
    public async Task<WalletFundingContext> LoadAsync(Guid userId, Guid walletId, bool business, bool write,
        bool requireDeposits, CancellationToken ct)
    {
        Guid owner; Guid? profileId = null; string country; string? email; string name; string? phone;
        if (business)
        {
            var access = await businessAccess.EnsurePermissionAsync(userId,
                write ? BusinessPermission.ViewWallets | BusinessPermission.ManageFunding : BusinessPermission.ViewWallets, ct);
            var profile = await db.BusinessProfiles.AsNoTracking().Include(x => x.OwnerUser)
                .SingleOrDefaultAsync(x => x.Id == access.BusinessProfileId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Business profile not found.");
            owner = profile.Id; country = profile.CountryCode; email = profile.ContactEmail ?? profile.OwnerUser.Email;
            name = profile.BusinessName; phone = profile.ContactPhone;
        }
        else
        {
            var profile = await db.CustomerProfiles.AsNoTracking().Include(x => x.User)
                .SingleOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Customer profile not found.");
            owner = userId; profileId = profile.Id; country = profile.CountryCode;
            email = profile.Email ?? profile.User.Email; name = $"{profile.FirstName} {profile.LastName}".Trim(); phone = profile.PhoneNumber;
        }
        var ownerType = business ? FinancialAccountOwnerType.Business : FinancialAccountOwnerType.User;
        var wallet = await db.FinancialAccounts.Include(x => x.Asset).SingleOrDefaultAsync(x => x.Id == walletId &&
            x.OwnerId == owner && x.OwnerType == ownerType && x.AccountType == FinancialAccountType.Customer && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Wallet not found.");
        if (requireDeposits && (wallet.Status != FinancialAccountStatus.Active || !wallet.Asset.IsSupported ||
            !wallet.Asset.DepositEnabled || wallet.Asset.Type != AssetType.Fiat ||
            (business ? !wallet.Asset.BusinessEnabled : !wallet.Asset.IndividualEnabled) ||
            !await db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == country && x.AssetCode == wallet.AssetCode &&
                x.Country.IsSupported && x.CanDeposit, ct)))
            throw new InvalidOperationException($"Deposits are not enabled for this {wallet.AssetCode} wallet in {country}.");
        return new(wallet, country, business, owner, profileId, email, name, phone);
    }

    public Task<ComplianceGateResult> EnsureComplianceAsync(WalletFundingContext context, CancellationToken ct) =>
        context.Business ? compliance.EnsureBusinessCanInitiateMoneyMovementAsync(context.OwnerId, ProviderCode.Blaaiz, ct)
        : compliance.EnsureCanInitiateMoneyMovementAsync(context.CustomerProfileId!.Value, ProviderCode.Blaaiz, ct);

    public async Task<VerifiedBusinessPrincipal> EnsureBusinessPrincipalReceivingAccountAsync(
        WalletFundingContext context, Guid actorUserId, bool principalHolderConsent, CancellationToken ct)
    {
        if (!context.Business || !BusinessPrincipalReceivingAccountPolicy.IsForeignVirtualAccountCurrency(context.Wallet.AssetCode))
            throw new InvalidOperationException("Primary-holder receiving accounts are only for USD, GBP and EUR business wallets.");

        // OwnerUserId is the authoritative primary owner, not an arbitrary team
        // member supplied by the browser. A finance/admin member cannot consent
        // on behalf of another natural person.
        var ownerUserId = await db.BusinessProfiles.AsNoTracking()
            .Where(x => x.Id == context.OwnerId && !x.IsDeleted)
            .Select(x => x.OwnerUserId).SingleAsync(ct);
        BusinessPrincipalReceivingAccountPolicy.RequireOwnerConsent(actorUserId, ownerUserId, principalHolderConsent);

        // Both the business KYB and the holder's separate individual KYC must
        // pass; their provider customer IDs must not be conflated.
        var businessApproval = await compliance.EnsureBusinessCanInitiateMoneyMovementAsync(
            context.OwnerId, ProviderCode.Blaaiz, ct);
        var principal = await db.CustomerProfiles.AsNoTracking()
            .Where(x => x.UserId == ownerUserId && !x.IsDeleted)
            .Select(x => new { x.Id, x.CustomerType }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The business's primary holder must complete their separate individual KYC before a foreign receiving account can be requested.");
        if (principal.CustomerType != CustomerType.Individual)
            throw new InvalidOperationException("The business's primary holder needs a verified Blaaiz individual customer profile, not another business profile.");
        var individualApproval = await compliance.EnsureCanInitiateMoneyMovementAsync(
            principal.Id, ProviderCode.Blaaiz, ct);
        if (string.Equals(individualApproval.ProviderCustomerId, businessApproval.ProviderCustomerId, StringComparison.Ordinal))
            throw new InvalidOperationException("The primary holder and business resolve to the same Blaaiz customer. Separate identity verification is required.");
        return new VerifiedBusinessPrincipal(ownerUserId, principal.Id, individualApproval);
    }
}
