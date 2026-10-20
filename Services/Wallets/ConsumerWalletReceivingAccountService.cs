using KorridorX.Data;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Dtos.Wallets;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Wallets;

/// <summary>
/// Starts the existing dedicated-account workflow after an explicit Add wallet action.
/// GET is read-only. No setup path creates a collection, transfer, or ledger posting.
/// </summary>
public sealed class ConsumerWalletReceivingAccountService(
    AppDbContext db,
    WalletFundingContextService contexts,
    PaymentChannelService channels,
    WalletDepositAccountService accounts,
    IProviderWalletResolver providerWallets,
    ILogger<ConsumerWalletReceivingAccountService> logger) : IConsumerWalletReceivingAccountService
{
    public async Task<ConsumerWalletReceivingAccountDto> GetAsync(Guid userId, Guid walletId, CancellationToken ct = default) =>
        (await InspectAsync(userId, walletId, ct)).Status;

    public async Task<ConsumerWalletReceivingAccountDto> SetupAsync(Guid userId, Guid walletId, CancellationToken ct = default)
    {
        var inspection = await InspectAsync(userId, walletId, ct);
        if (!inspection.CanSetup || inspection.ChannelId is not { } channelId) return inspection.Status;

        try
        {
            // The existing service commits the selected connection/route before external creation,
            // serializes duplicate requests and looks up an uncertain result instead of creating twice.
            var result = await accounts.ProvisionAsync(userId, walletId, channelId, false, ct);
            return Describe(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is ProviderIntegrationException or HttpRequestException or
            OperationCanceledException or InvalidOperationException)
        {
            logger.LogWarning("Receiving-account setup did not complete for wallet {WalletId}; error type {ErrorType}.",
                walletId, error.GetType().Name);
            // Provisioning records Creating before the request. Preserve that durable outcome and
            // never convert an unknown provider result into permission for another creation.
            var row = await db.WalletDepositAccounts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.FinancialAccountId == walletId && x.PaymentChannelId == channelId &&
                x.Environment == channels.EnvironmentName && x.ConnectionKey == providerWallets.ConnectionKey && !x.IsDeleted, ct);
            if (row is not null) return Describe(WalletDepositAccountService.ToDto(row, channelId));
            return new("ActionRequired", "Your wallet was added. Receiving-account setup could not be completed. Check the account status or contact support.",
                channelId, null, true);
        }
    }

    private async Task<Inspection> InspectAsync(Guid userId, Guid walletId, CancellationToken ct)
    {
        // Ownership is checked before looking up any route or receiving-account identity.
        var context = await contexts.LoadAsync(userId, walletId, false, false, false, ct);
        if (context.Wallet.Status != FinancialAccountStatus.Active)
            return Blocked("Unavailable", "This wallet must be active before a receiving account can be set up.");
        if (!BlaaizChannelCapabilities.All.Any(x => x.AssetCode == context.Wallet.AssetCode &&
            x.Code == BlaaizChannelCapabilities.VirtualAccount && x.IndividualSupported && x.Implemented))
            return Blocked("Unavailable", "This wallet uses its available funding methods instead of a dedicated bank account.");

        var existing = await db.WalletDepositAccounts.AsNoTracking().Where(x => x.FinancialAccountId == walletId &&
            x.Environment == channels.EnvironmentName && !x.IsDeleted).ToListAsync(ct);
        if (existing.Any(x => x.ConnectionKey != providerWallets.ConnectionKey))
            return Blocked("ActionRequired", "A receiving account belongs to a previous provider connection. Support must reconcile it before setup can continue.");
        if (existing.Count > 1)
            return Blocked("ActionRequired", "Several receiving accounts exist for this wallet. Select the required bank-deposit option to view its status.");

        var choices = (await channels.EligibleAsync(context.CountryCode, context.Wallet.AssetCode, false,
            PaymentChannelDirection.Deposit, null, 0m, ct))
            .Where(x => x.Capability.Code == BlaaizChannelCapabilities.VirtualAccount).ToArray();
        if (choices.Length == 0)
            return Blocked("Unavailable", "Bank deposits are not enabled for this wallet yet. Contact support for available funding methods.");

        PaymentChannelChoice? selected;
        var saved = existing.SingleOrDefault();
        if (saved is not null)
        {
            selected = choices.FirstOrDefault(x => x.Wallet.Id == saved.ProviderWalletConfigurationId &&
                x.Wallet.ProviderWalletId == saved.ProviderWalletId);
            if (selected is null)
                return Blocked("Unavailable", "The configured bank-deposit route is unavailable. Support must restore it; your receiving account will not be moved to another route.");
        }
        else
        {
            var routes = choices.GroupBy(x => x.Wallet.Id).Select(x => x.First()).ToArray();
            var defaults = routes.Where(x => x.Wallet.DefaultForCollection).ToArray();
            selected = routes.Length == 1 ? routes[0] : defaults.Length == 1 ? defaults[0] : null;
            if (selected is null)
                return Blocked("ActionRequired", "Several bank-deposit routes are available. Select a bank-deposit option, or ask support to configure the default receiving route.");
        }

        try { await contexts.EnsureComplianceAsync(context, ct); }
        catch (InvalidOperationException)
        {
            return new(new("VerificationRequired", "Identity verification must be approved by KorridorX and the banking provider before your receiving account can be opened.",
                selected.Channel.Id, null, false), selected.Channel.Id, false);
        }

        if (saved is null)
            return new(new("NotStarted", "Your receiving account is ready to be requested. Setup starts automatically when you add this wallet or open Add money.",
                selected.Channel.Id, null, true), selected.Channel.Id, true);
        var status = Describe(WalletDepositAccountService.ToDto(saved, selected.Channel.Id));
        return new(status, selected.Channel.Id, saved.Status is "Creating" or "Pending" || saved.CanRetryCreation);
    }

    private static ConsumerWalletReceivingAccountDto Describe(WalletDepositAccountDto account) =>
        account.Status switch
        {
            "Active" when account.Instructions.IsReady => new("Ready", "Your receiving account is ready. Use the bank details below to add money.", account.ChannelId, account, false),
            "Creating" or "Pending" => new("Pending", "Your banking provider is preparing the receiving account. Check its status again shortly; bank details appear after confirmation.", account.ChannelId, account, false),
            _ => new("ActionRequired", account.FailureReason ?? "Receiving-account setup needs attention. Contact support before sending money.", account.ChannelId, account, account.CanRetry)
        };

    private static Inspection Blocked(string status, string message) => new(new(status, message, null, null, false), null, false);
    private sealed record Inspection(ConsumerWalletReceivingAccountDto Status, Guid? ChannelId, bool CanSetup);
}
