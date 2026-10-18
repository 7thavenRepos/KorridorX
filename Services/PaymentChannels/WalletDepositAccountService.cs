using System.Data;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Dtos.Payments;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Providers;
using KorridorX.Services.Wallets;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.PaymentChannels;

/// <summary>Dedicated receiving accounts. Provisioning never credits a wallet.</summary>
public sealed class WalletDepositAccountService(AppDbContext db, PaymentChannelService channels,
    WalletFundingContextService contexts, IProviderWalletResolver wallets, IBlaaizApiClient api,
    IRemittanceProvider provider, IConsumerWalletFundingCreditService credits)
{
    public async Task<WalletDepositAccountDto?> GetAsync(Guid user, Guid walletId, Guid channelId, bool business,
        bool refresh, CancellationToken ct)
    {
        await contexts.LoadAsync(user, walletId, business, false, false, ct);
        var channel = await db.PaymentChannels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == channelId && x.Environment == channels.EnvironmentName, ct);
        if (channel is null) return null;
        var row = await db.WalletDepositAccounts.SingleOrDefaultAsync(x => x.FinancialAccountId == walletId &&
            x.ProviderWalletConfigurationId == channel.ProviderWalletConfigurationId && x.Environment == channels.EnvironmentName && !x.IsDeleted, ct);
        if (row is null) return null;
        EnsureConnection(row);
        if (refresh)
        {
            var found = await FetchStateAsync(row, ct);
            if (found is not null) { ApplyState(row, found); await db.SaveChangesAsync(ct); }
        }
        return ToDto(row, channelId);
    }

    public async Task<WalletDepositAccountDto> ProvisionAsync(Guid user, Guid walletId, Guid channelId,
        bool business, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, walletId, business, true, true, ct);
        var choice = await channels.RequireDepositAsync(channelId, context.CountryCode, context.Wallet.AssetCode,
            business, true, PaymentMethod.VirtualAccount, 0, ct)
            ?? throw new InvalidOperationException("Select an active bank-account deposit channel.");
        var compliance = await contexts.EnsureComplianceAsync(context, ct);
        WalletDepositAccount row; var newRequest = false;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            var lockKey = "WalletDepositAccount:" + channels.EnvironmentName + ":" + choice.Wallet.ProviderWalletId + ":" + compliance.ProviderCustomerId;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
            var existing = await db.WalletDepositAccounts.SingleOrDefaultAsync(x => x.Environment == channels.EnvironmentName &&
                x.ProviderWalletId == choice.Wallet.ProviderWalletId && x.ProviderCustomerId == compliance.ProviderCustomerId, ct);
            if (existing is not null)
            {
                if (existing.IsDeleted || existing.FinancialAccountId != walletId) throw new InvalidOperationException("This provider customer/wallet already belongs to another deposit account. Contact support; it will not be reassigned.");
                EnsureConnection(existing); row = existing;
                if (row.Status == "Active") { await tx.CommitAsync(ct); return ToDto(row, channelId); }
                if (row.CanRetryCreation)
                {
                    row.CanRetryCreation = false; row.Status = "Creating"; row.FailureReason = null;
                    row.LastUpdatedAt = DateTime.UtcNow; newRequest = true;
                }
            }
            else
            {
                row = new WalletDepositAccount { FinancialAccountId = walletId, PaymentChannelId = channelId,
                    ProviderWalletConfigurationId = choice.Wallet.Id, ProviderWalletId = choice.Wallet.ProviderWalletId,
                    ProviderCustomerId = compliance.ProviderCustomerId, AssetCode = context.Wallet.AssetCode,
                    Environment = channels.EnvironmentName, ConnectionKey = wallets.ConnectionKey, CreatedByUserId = user };
                db.WalletDepositAccounts.Add(row);
                channels.Record(choice, "WalletDepositAccount", row.Id, context.CountryCode, business, context.OwnerId, walletId, null);
                newRequest = true;
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // Route is committed BEFORE external submission. A timeout only permits a lookup,
        // not a second creation. Explicit provider rejections may be retried after lookup.
        if (!newRequest)
        {
            var current = await FetchStateAsync(row, ct);
            if (current is not null) { ApplyState(row, current); await db.SaveChangesAsync(ct); }
            return ToDto(row, channelId);
        }
        try
        {
            await channels.ValidateInitiationAsync("WalletDepositAccount", row.Id, ct);
            var selectedWallet = await wallets.SelectAsync("WalletDepositAccount", row.Id, "BLAAIZ", row.AssetCode,
                null, ProviderWalletResolver.Collection, false, ct);
            if (selectedWallet != row.ProviderWalletId) throw new InvalidOperationException("Provider wallet selection changed. Provisioning was stopped.");
        }
        catch
        {
            // Nothing has been submitted externally. Permit a guarded retry
            // after configuration is restored; it still performs a lookup first.
            row.Status = "Failed"; row.CanRetryCreation = true; row.LastCheckedAt = DateTime.UtcNow;
            row.FailureReason = "The selected channel or provider wallet is unavailable. No account creation request was sent.";
            await db.SaveChangesAsync(CancellationToken.None); throw;
        }
        try
        {
            // Check an existing account first on retry. New customer/wallet pairs use creation once.
            if (row.LastCheckedAt.HasValue)
            {
                var previous = await FetchStateAsync(row, ct);
                if (previous is not null) { ApplyState(row, previous); await db.SaveChangesAsync(ct); return ToDto(row, channelId); }
            }
            var response = await api.CreateVirtualBankAccountAsync(new BlaaizVirtualBankAccountRequest
                { WalletId = row.ProviderWalletId, CustomerId = row.ProviderCustomerId }, context.CustomerProfileId ?? context.OwnerId, ct);
            ApplyState(row, BlaaizVirtualAccountState.ParseResponse(response.RawResponseJson, row.ProviderWalletId, row.ProviderCustomerId, row.AssetCode));
            await db.SaveChangesAsync(ct); return ToDto(row, channelId);
        }
        catch (ProviderIntegrationException ex)
        {
            row.LastCheckedAt = DateTime.UtcNow;
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                var current = await FetchStateAsync(row, ct);
                if (current is not null) { ApplyState(row, current); await db.SaveChangesAsync(ct); return ToDto(row, channelId); }
            }
            row.CanRetryCreation = ex.ProviderStatusCode is 400 or 401 or 403 or 404 or 422;
            row.Status = row.CanRetryCreation ? "Failed" : "Creating";
            row.FailureReason = row.CanRetryCreation ? "The provider rejected account setup. Review verification and required customer fields with support, then retry." : "Provider outcome is not yet known. Check account status; do not create a duplicate.";
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<BlaaizVirtualAccountState?> FetchStateAsync(WalletDepositAccount row, CancellationToken ct)
    {
        EnsureConnection(row);
        var response = await api.GetVirtualBankAccountsAsync(row.ProviderWalletId, row.ProviderCustomerId, row.FinancialAccountId, ct);
        using var doc = JsonDocument.Parse(response.RawResponseJson);
        var data = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() == 0) return null;
        return BlaaizVirtualAccountState.ParseResponse(response.RawResponseJson, row.ProviderWalletId, row.ProviderCustomerId, row.AssetCode);
    }

    private void EnsureConnection(WalletDepositAccount row)
    {
        if (row.Environment != channels.EnvironmentName || row.ConnectionKey != wallets.ConnectionKey)
            throw new InvalidOperationException("This receiving account belongs to a previous provider connection. Reconcile it before further use; bank details are not being reused across connections.");
    }

    public static void ApplyState(WalletDepositAccount row, BlaaizVirtualAccountState state)
    {
        if (row.ProviderAccountId is not null && row.ProviderAccountId != state.Id ||
            state.CustomerId is not null && state.CustomerId != row.ProviderCustomerId ||
            state.WalletId is not null && state.WalletId != row.ProviderWalletId ||
            state.Currency is not null && state.Currency != row.AssetCode)
            throw new InvalidOperationException("The provider account identity does not match its saved owner and wallet.");
        if (row.Status == "Active") return; // stale failures must not erase confirmed bank details
        row.ProviderAccountId = state.Id; row.LastCheckedAt = DateTime.UtcNow; row.LastUpdatedAt = DateTime.UtcNow;
        row.Status = state.Status switch { ProviderAccountMappingStatus.Active => "Active", ProviderAccountMappingStatus.Failed => "Failed", _ => "Pending" };
        row.CanRetryCreation = false;
        row.FailureReason = row.Status == "Failed" ? state.FailureReason ?? "Provider account setup was rejected. Review the customer details with support." : null;
        // Never disclose bank details until the provider explicitly confirms readiness.
        row.AccountNumber = row.Status == "Active" ? state.AccountNumber : null;
        row.AccountName = row.Status == "Active" ? state.AccountName : null;
        row.BankName = row.Status == "Active" ? state.BankName : null;
        row.Iban = row.Status == "Active" ? state.Iban : null;
        row.SortCode = row.Status == "Active" ? state.SortCode : null;
        row.RoutingNumber = row.Status == "Active" ? state.RoutingNumber : null;
        row.ProviderReference = state.Reference;
    }

    public async Task<bool> TryProcessAccountEventAsync(string eventType, string raw, CancellationToken ct)
    {
        var state = BlaaizVirtualAccountState.ParseWebhook(raw, eventType);
        var row = await db.WalletDepositAccounts.SingleOrDefaultAsync(x => x.Environment == channels.EnvironmentName &&
            x.ConnectionKey == wallets.ConnectionKey && !x.IsDeleted && x.ProviderWalletId == state.WalletId &&
            x.ProviderCustomerId == state.CustomerId && x.AssetCode == state.Currency, ct);
        if (row is null) return false;
        ApplyState(row, state); await db.SaveChangesAsync(ct); return true;
    }

    // Called ONLY by the signature-verified webhook processor. Exact account ID is
    // required; a customer's email/name or a pooled wallet is never an ownership key.
    public async Task<bool> TryProcessDepositAsync(EmbeddedInboundCollectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderAccountId) || string.IsNullOrWhiteSpace(request.ProviderTransactionId)) return false;
        var row = await db.WalletDepositAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Environment == channels.EnvironmentName &&
            x.ConnectionKey == wallets.ConnectionKey && x.ProviderAccountId == request.ProviderAccountId && !x.IsDeleted, ct);
        if (row is null) return false;
        if (!string.IsNullOrWhiteSpace(request.ProviderCustomerId) && request.ProviderCustomerId != row.ProviderCustomerId)
            throw new InvalidOperationException("The deposit customer does not match the receiving account.");
        if (!string.IsNullOrWhiteSpace(request.AccountNumber) && row.AccountNumber is not null && request.AccountNumber != row.AccountNumber)
            throw new InvalidOperationException("The deposit account number does not match the receiving account.");
        if (request.ProviderStatus.ToUpperInvariant() is not ("SUCCESSFUL" or "COMPLETED")) return true;
        if (row.Status != "Active") throw new InvalidOperationException("The receiving account is not ready; retain the webhook for review/retry.");
        var verified = await provider.GetTransactionAsync(request.ProviderTransactionId, ct: ct);
        ValidateVerifiedDeposit(row, request, verified);
        // The financial update and provider receipt commit atomically. Separate
        // transaction-ID and wallet locks protect both replay and concurrent deposits.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var key = "WalletBankDeposit:" + request.ProviderTransactionId;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            var account = await db.FinancialAccounts.FromSqlInterpolated($"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {row.FinancialAccountId} FOR UPDATE").SingleAsync(ct);
            // Reload an already tracked entity; the SQL row lock alone does not refresh EF's identity map.
            await db.Entry(account).ReloadAsync(ct);
            if (account.IsDeleted || account.Status != FinancialAccountStatus.Active || account.AssetCode != row.AssetCode ||
                account.AccountType != FinancialAccountType.Customer || account.OwnerType is not (FinancialAccountOwnerType.User or FinancialAccountOwnerType.Business))
                throw new InvalidOperationException("Receiving wallet is unavailable. Deposit retained for reconciliation; no balance was changed.");
            var ownerApproved = account.OwnerType == FinancialAccountOwnerType.User
                ? await db.CustomerProfiles.AsNoTracking().AnyAsync(x => x.UserId == account.OwnerId && !x.IsDeleted && x.KycStatus == KycStatus.Approved, ct)
                : await db.BusinessProfiles.AsNoTracking().AnyAsync(x => x.Id == account.OwnerId && !x.IsDeleted && x.KybStatus == KybStatus.Approved, ct);
            if (!ownerApproved) throw new InvalidOperationException("Receiving owner requires compliance review. The webhook is retained; no balance was changed.");
            var existing = await db.ProviderTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderCode == ProviderCode.Blaaiz && x.ProviderTransactionId == request.ProviderTransactionId, ct);
            if (existing?.CollectionId is not null)
            {
                var replay = await db.Collections.AsNoTracking().SingleAsync(x => x.Id == existing.CollectionId, ct);
                if (replay.FinancialAccountId != row.FinancialAccountId || replay.Amount != verified.Amount || replay.CurrencyCode != row.AssetCode ||
                    replay.RelatedEntityType != nameof(WalletDepositAccount) || replay.RelatedEntityId != row.Id)
                    throw new InvalidOperationException("The provider transaction already belongs to a different collection.");
                await tx.CommitAsync(ct); return true;
            }
            if (existing is not null) throw new InvalidOperationException("Provider transaction exists without a completed wallet deposit; reconciliation is required.");
            var channelName = await db.PaymentChannels.AsNoTracking().Where(x => x.Id == row.PaymentChannelId).Select(x => x.DisplayName).SingleAsync(ct);
            var collection = new Collection { Purpose = PaymentOperationPurpose.AccountFunding,
                FinancialAccountId = account.Id, RelatedEntityType = nameof(WalletDepositAccount), RelatedEntityId = row.Id,
                ContextEntityType = nameof(FinancialAccount), ContextEntityId = account.Id,
                Reference = $"COL-VA-{Guid.NewGuid():N}", CurrencyCode = row.AssetCode, Amount = verified.Amount,
                PaymentMethod = PaymentMethod.VirtualAccount, PaymentChannelId = row.PaymentChannelId, PaymentChannelName = channelName,
                Status = CollectionStatus.Successful, ProviderCode = "BLAAIZ", ProviderCollectionId = request.ProviderTransactionId,
                ProviderReference = verified.ProviderReference, InitiatedAt = DateTime.UtcNow, ConfirmedAt = DateTime.UtcNow };
            db.Collections.Add(collection);
            await credits.ApplySuccessfulCollectionAsync(collection, null, "VerifiedBankDeposit", ct);
            db.ProviderTransactions.Add(new ProviderTransaction { ProviderCode = ProviderCode.Blaaiz,
                ProviderTransactionId = request.ProviderTransactionId, CollectionId = collection.Id, Collection = collection,
                ProviderReference = verified.ProviderReference, TransactionType = "collection", ProviderStatus = verified.ProviderStatus,
                CurrencyCode = row.AssetCode, Amount = verified.Amount, AmountWithoutFee = request.AmountWithoutFee,
                ProviderFeeAmount = request.ProviderFeeAmount, ProviderFeeCurrencyCode = request.ProviderFeeCurrencyCode,
                RawPayloadJson = verified.RawResponseJson, LastSyncedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            // The outer webhook handler will save its FAILED receipt. Do not allow
            // that SaveChanges to accidentally persist our rolled-back tracked money.
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is Collection or LedgerTransaction or LedgerPosting or ProviderTransaction or FinancialAccount).ToArray())
            {
                if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
                else if (entry.State is EntityState.Modified or EntityState.Deleted) await entry.ReloadAsync(CancellationToken.None);
            }
            throw;
        }
    }

    public static void ValidateVerifiedDeposit(WalletDepositAccount account, EmbeddedInboundCollectionRequest request, RemittanceTransactionStatusResult verified)
    {
        if (verified.ProviderTransactionId != request.ProviderTransactionId ||
            verified.TransactionType.ToLowerInvariant() != "collection" ||
            verified.ProviderStatus.ToUpperInvariant() is not ("SUCCESSFUL" or "COMPLETED") ||
            verified.CurrencyCode.ToUpperInvariant() != account.AssetCode || request.CurrencyCode.ToUpperInvariant() != account.AssetCode ||
            request.Amount <= 0 || verified.Amount != request.Amount)
            throw new InvalidOperationException("Provider transaction verification did not match the deposit identity, amount, currency and successful status.");
        // Validate account/customer/wallet identifiers when the verification response includes them.
        using var document = JsonDocument.Parse(verified.RawResponseJson);
        var data = document.RootElement.TryGetProperty("data", out var d) ? d : document.RootElement;
        foreach (var pair in new[] { ("customer_id", account.ProviderCustomerId), ("wallet_id", account.ProviderWalletId),
                     ("business_wallet_id", account.ProviderWalletId), ("virtual_account_id", account.ProviderAccountId), ("virtual_bank_account_id", account.ProviderAccountId) })
            if (data.TryGetProperty(pair.Item1, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() != pair.Item2)
                throw new InvalidOperationException("Verified provider transaction identifies a different receiving account.");
    }

    public static WalletDepositAccountDto ToDto(WalletDepositAccount row, Guid? requestedChannel = null)
    {
        var ready = row.Status == "Active"; var fields = new List<FundingInstructionFieldDto>();
        void Add(string key, string label, string? value) { if (ready && !string.IsNullOrWhiteSpace(value)) fields.Add(new(key, label, value)); }
        Add("bank_name", "Bank", row.BankName); Add("account_name", "Account name", row.AccountName);
        Add("account_number", "Account number", row.AccountNumber); Add("iban", "IBAN", row.Iban);
        Add("sort_code", "Sort code", row.SortCode); Add("routing_number", "Routing number", row.RoutingNumber);
        // Never label a local collection ID or provider transaction ID as a payment reference.
        return new(row.Id, row.FinancialAccountId, requestedChannel ?? row.PaymentChannelId, row.AssetCode, row.Status,
            row.CanRetryCreation, row.FailureReason, new("BankAccount", row.AssetCode, "Your deposit bank details",
                ready ? "Use only these verified details and send the account currency. Your balance updates after a confirmed deposit, not after account creation."
                    : row.FailureReason ?? "The bank account is not ready. Check status; do not send funds until verified details appear.",
                ready, fields, ready ? ["Send the account currency from an eligible bank account.", "Wait for the provider to confirm the deposit, then refresh your wallet.", "Contact support with the deposit reference if a confirmed payment is missing."] : []));
    }
    private static string Clean(string value, int max) => value.Length <= max ? value : value[..max];
}
