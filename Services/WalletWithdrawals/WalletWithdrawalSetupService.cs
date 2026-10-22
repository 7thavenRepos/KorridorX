using System.Data;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Audit;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.WalletWithdrawals;

// Phase W1 deliberately contains NO balance mutation and NO payout dispatch.
// Live movement is introduced only after fee-review, reservation, webhook and reversal gates.
public sealed class WalletWithdrawalSetupService : IWalletWithdrawalSetupService
{
    private const string SecurityEntity = "AccountSecurity.WalletWithdrawalDestination";
    private readonly AppDbContext _db;
    private readonly WalletFundingContextService _contexts;
    private readonly PaymentChannelService _channels;
    private readonly IRemittanceProvider _provider;
    private readonly ITransactionPinService _pin;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _audit;
    private readonly IDataProtector _protector;

    public WalletWithdrawalSetupService(AppDbContext db, WalletFundingContextService contexts,
        PaymentChannelService channels, IRemittanceProvider provider, ITransactionPinService pin,
        INotificationQueueService notifications, IAuditService audit, IDataProtectionProvider protection)
    {
        _db = db; _contexts = contexts; _channels = channels; _provider = provider;
        _pin = pin; _notifications = notifications; _audit = audit;
        _protector = protection.CreateProtector("KorridorX.FiatWalletWithdrawalDestination.AccountNumber.v1");
    }

    public async Task<IReadOnlyList<WalletWithdrawalMethodDto>> GetMethodsAsync(
        Guid userId, Guid walletId, bool business, CancellationToken ct = default)
    {
        var context = await _contexts.LoadAsync(userId, walletId, business, write: false, requireDeposits: false, ct);
        return await MethodsAsync(context, ct);
    }

    public async Task<IReadOnlyList<WalletWithdrawalDestinationDto>> GetDestinationsAsync(
        Guid userId, Guid walletId, bool business, CancellationToken ct = default)
    {
        var context = await _contexts.LoadAsync(userId, walletId, business, write: false, requireDeposits: false, ct);
        var rows = await _db.FiatWalletWithdrawalDestinations.AsNoTracking()
            .Where(x => x.FinancialAccountId == walletId && x.OwnerType == context.Wallet.OwnerType &&
                        x.OwnerId == context.OwnerId && !x.IsDeleted && x.IsActive)
            .OrderByDescending(x => x.VerifiedAtUtc).ToListAsync(ct);
        return rows.Select(ToDto).ToArray();
    }

    public async Task<WalletWithdrawalDestinationDto> SaveDestinationAsync(
        Guid userId, Guid walletId, bool business, SaveWalletWithdrawalDestinationRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null) throw new InvalidOperationException("Withdrawal destination is required.");
        var context = await _contexts.LoadAsync(userId, walletId, business, write: true, requireDeposits: false, ct);
        await RequireWalletWithdrawalAllowedAsync(context, ct);
        if (!business) await _pin.VerifyForTransactionAsync(userId, request.TransactionPin, ct);
        await _contexts.EnsureComplianceAsync(context, ct);

        var asset = context.Wallet.AssetCode;
        var country = WalletWithdrawalSetupPolicy.DestinationCountry(asset, request.PaymentMethod);
        var methods = await MethodsAsync(context, ct);
        if (!methods.Any(x => x.CurrencyCode == asset && x.DestinationCountryCode == country &&
            x.PaymentMethod == request.PaymentMethod))
            throw new InvalidOperationException("No verified and active same-currency withdrawal channel supports this method. Configure a payout channel in Admin.");

        string? bankId = null, bankName = null, accountNumber = null, verifiedName = null;
        string? email = null, verification = null;
        if (request.PaymentMethod == PaymentMethod.BankTransfer)
        {
            if (!string.IsNullOrWhiteSpace(request.Email))
                throw new InvalidOperationException("Bank withdrawals do not accept an Interac email.");
            accountNumber = WalletWithdrawalSetupPolicy.NormalizeNgnAccountNumber(request.AccountNumber);
            bankId = request.ProviderBankId?.Trim();
            if (string.IsNullOrWhiteSpace(bankId))
                throw new InvalidOperationException("Choose a verified bank from the NGN bank directory.");
            var bank = await _db.ProviderBanks.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ProviderCode == ProviderCode.Blaaiz && x.ProviderBankId == bankId &&
                x.CountryCode == "NG" && x.IsActive && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("The chosen Nigerian bank is not active in the provider bank directory.");
            var result = await _provider.ResolveBankAccountAsync(bank.ProviderBankId, accountNumber, ct);
            if (!string.Equals(result.ProviderBankId, bank.ProviderBankId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(result.AccountNumber, accountNumber, StringComparison.Ordinal))
                throw new InvalidOperationException("Bank verification returned a different account. No destination was saved.");
            if (!WalletWithdrawalSetupPolicy.IsOwnAccount(context.Name, result.AccountName, context.Business))
                throw new InvalidOperationException("The verified bank-account name does not match the wallet owner's identity. Use Send for payments to other people.");
            bankName = bank.Name;
            verifiedName = result.AccountName;
            verification = result.ProviderRequestLogId.ToString();
        }
        else if (request.PaymentMethod == PaymentMethod.Interac)
        {
            if (!string.IsNullOrWhiteSpace(request.ProviderBankId) || !string.IsNullOrWhiteSpace(request.AccountNumber))
                throw new InvalidOperationException("Interac withdrawals use a verified email, not bank account fields.");
            email = WalletWithdrawalSetupPolicy.NormalizeVerifiedEmail(request.Email);
            var ownerUserId = business
                ? await _db.BusinessProfiles.AsNoTracking().Where(x => x.Id == context.OwnerId)
                    .Select(x => x.OwnerUserId).SingleAsync(ct)
                : userId;
            var confirmedEmail = await _db.Users.AsNoTracking()
                .Where(x => x.Id == ownerUserId && x.EmailConfirmed)
                .Select(x => x.Email).SingleOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(confirmedEmail) ||
                !string.Equals(email, confirmedEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Interac withdrawals require the account owner's confirmed KorridorX email. Use Send for another email.");
            verifiedName = context.Name;
            verification = "KorridorXConfirmedOwnerEmail";
        }
        else throw new InvalidOperationException("Unsupported wallet withdrawal method.");

        // Provider verification is read-only and deliberately completed before the DB transaction.
        // Serialize writes for this wallet; repeat setup is idempotent and never creates a payout.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var existing = await _db.FiatWalletWithdrawalDestinations
                .Where(x => x.FinancialAccountId == walletId && x.OwnerType == context.Wallet.OwnerType &&
                    x.OwnerId == context.OwnerId && x.PaymentMethod == request.PaymentMethod && !x.IsDeleted).ToListAsync(ct);
            var row = existing.FirstOrDefault(x => request.PaymentMethod == PaymentMethod.Interac
                ? string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)
                : x.ProviderBankId == bankId && x.ProtectedAccountNumber is not null &&
                  string.Equals(_protector.Unprotect(x.ProtectedAccountNumber), accountNumber, StringComparison.Ordinal));
            if (row is not null && row.IsActive)
            {
                await tx.CommitAsync(ct);
                return ToDto(row);
            }
            row ??= new FiatWalletWithdrawalDestination
            {
                FinancialAccountId = walletId, OwnerType = context.Wallet.OwnerType, OwnerId = context.OwnerId,
                CreatedByUserId = userId
            };
            if (_db.Entry(row).State == EntityState.Detached) _db.FiatWalletWithdrawalDestinations.Add(row);
            row.AssetCode = asset; row.DestinationCountryCode = country;
            row.PaymentMethod = request.PaymentMethod;
            row.ProviderBankId = bankId; row.BankName = bankName;
            row.ProtectedAccountNumber = accountNumber is null ? null : _protector.Protect(accountNumber);
            row.AccountNumberLast4 = accountNumber is null ? null : accountNumber[^4..];
            row.VerifiedAccountName = verifiedName; row.Email = email;
            row.VerificationReference = verification; row.VerifiedAtUtc = DateTime.UtcNow;
            row.IsActive = true; row.LastUpdatedAt = DateTime.UtcNow; row.LastUpdatedByUserId = userId;

            var summary = ToDto(row);
            _audit.Stage(new AuditRecordRequest("WalletWithdrawalDestinationVerified", "AccountSecurity",
                nameof(FiatWalletWithdrawalDestination), row.Id.ToString(), NewValues: summary,
                UserId: userId));
            var subject = $"A {asset} withdrawal destination was added";
            var message = $"<p>A withdrawal destination was verified for your {asset} wallet.</p>" +
                $"<p>Method: {request.PaymentMethod}; Destination: {summary.MaskedAccountNumber ?? "confirmed account email"}.</p>" +
                "<p>If you did not make this change, contact KorridorX support immediately.</p>";
            if (business) await _notifications.QueueBusinessAsync(context.OwnerId, subject, message, SecurityEntity, row.Id, ct);
            else await _notifications.QueueUserAsync(userId, subject, message, SecurityEntity, row.Id, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return summary;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<WalletWithdrawalDestinationDto> DisableDestinationAsync(Guid userId, Guid walletId,
        Guid destinationId, bool business, DisableWalletWithdrawalDestinationRequestDto request,
        CancellationToken ct = default)
    {
        var context = await _contexts.LoadAsync(userId, walletId, business, write: true, requireDeposits: false, ct);
        if (!business) await _pin.VerifyForTransactionAsync(userId, request?.TransactionPin, ct);
        var row = await _db.FiatWalletWithdrawalDestinations.SingleOrDefaultAsync(x => x.Id == destinationId &&
            x.FinancialAccountId == walletId && x.OwnerId == context.OwnerId && x.OwnerType == context.Wallet.OwnerType && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Withdrawal destination not found.");
        if (!row.IsActive) return ToDto(row);
        row.IsActive = false; row.LastUpdatedAt = DateTime.UtcNow; row.LastUpdatedByUserId = userId;
        var summary = ToDto(row);
        _audit.Stage(new AuditRecordRequest("WalletWithdrawalDestinationDisabled", "AccountSecurity",
            nameof(FiatWalletWithdrawalDestination), row.Id.ToString(), NewValues: summary, UserId: userId));
        var subject = $"A {row.AssetCode} withdrawal destination was removed";
        const string message = "<p>A saved withdrawal destination has been disabled.</p><p>If you did not make this change, contact KorridorX support immediately.</p>";
        if (business) await _notifications.QueueBusinessAsync(context.OwnerId, subject, message, SecurityEntity, row.Id, ct);
        else await _notifications.QueueUserAsync(userId, subject, message, SecurityEntity, row.Id, ct);
        await _db.SaveChangesAsync(ct);
        return summary;
    }

    private async Task<IReadOnlyList<WalletWithdrawalMethodDto>> MethodsAsync(
        WalletFundingContext context, CancellationToken ct)
    {
        if (!await IsWalletEligibleAsync(context, ct)) return [];
        string destinationCountry;
        PaymentMethod method;
        try
        {
            method = context.Wallet.AssetCode == "NGN" ? PaymentMethod.BankTransfer : PaymentMethod.Interac;
            destinationCountry = WalletWithdrawalSetupPolicy.DestinationCountry(context.Wallet.AssetCode, method);
        }
        catch (InvalidOperationException) { return []; }
        var eligible = await _channels.EligibleAsync(destinationCountry, context.Wallet.AssetCode, context.Business,
            PaymentChannelDirection.Withdrawal, context.Wallet.AssetCode, null, ct);
        return eligible.Where(x => x.Capability.PaymentMethod == method &&
                x.Channel.AssetCode == context.Wallet.AssetCode && x.Wallet.AssetCode == context.Wallet.AssetCode)
            .Select(x => new WalletWithdrawalMethodDto(x.Channel.Id, context.Wallet.AssetCode,
                destinationCountry, method, x.Channel.DisplayName, x.Channel.MinimumAmount,
                x.Channel.MaximumAmount)).ToArray();
    }

    private async Task<bool> IsWalletEligibleAsync(WalletFundingContext context, CancellationToken ct)
    {
        var wallet = context.Wallet;
        return wallet.Status == FinancialAccountStatus.Active && wallet.Asset.Type == AssetType.Fiat &&
            wallet.Asset.IsSupported && wallet.Asset.WithdrawalEnabled &&
            (context.Business ? wallet.Asset.BusinessEnabled : wallet.Asset.IndividualEnabled) &&
            await _db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == context.CountryCode &&
                x.AssetCode == wallet.AssetCode && x.Country.IsSupported && x.Asset.IsSupported &&
                x.CanWithdraw, ct);
    }

    private async Task RequireWalletWithdrawalAllowedAsync(WalletFundingContext context, CancellationToken ct)
    {
        if (!await IsWalletEligibleAsync(context, ct))
            throw new InvalidOperationException("Withdrawal is not enabled for this wallet and customer profile.");
    }

    private static WalletWithdrawalDestinationDto ToDto(FiatWalletWithdrawalDestination row) => new(
        row.Id, row.FinancialAccountId, row.AssetCode, row.DestinationCountryCode,
        row.PaymentMethod, row.PaymentMethod == PaymentMethod.Interac ? "Interac e-Transfer" : "Bank transfer",
        row.BankName, row.AccountNumberLast4 is null ? null : "•••• " + row.AccountNumberLast4,
        row.VerifiedAccountName, row.Email, row.IsActive, row.VerifiedAtUtc);
}
