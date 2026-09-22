using System.Net.Mail;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Providers;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class BusinessCollectionAccountService(
    AppDbContext db, IBusinessAccessService access, CollectionAccountProvisioningService provisioning,
    IEnumerable<ICollectionAccountProvisioner> provisioners, IProviderWalletResolver wallets,
    IEmbeddedWebhookOutboxStager outbox, IOptions<BlaaizOptions> options)
{
    private const BusinessPermission Read = BusinessPermission.ViewEmbeddedFinance;
    private const BusinessPermission Write = Read | BusinessPermission.ManageEmbeddedCustomers;

    public async Task<PagedResult<BusinessCollectionCustomerDto>> GetCustomersAsync(
        Guid userId, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var actor = await access.EnsurePermissionAsync(userId, Read, ct);
        var query = db.BusinessCustomers.AsNoTracking().Where(x => x.BusinessProfileId == actor.BusinessProfileId && !x.IsDeleted);
        var term = (search ?? "").Trim();
        if (term.Length > 150) throw new InvalidOperationException("Search cannot exceed 150 characters.");
        if (term.Length > 0)
            query = query.Where(x => x.DisplayName.Contains(term) || x.ExternalReference.Contains(term));
        return await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
            .Select(x => new BusinessCollectionCustomerDto(x.Id, x.ExternalReference, x.DisplayName, x.Email,
                x.PhoneNumber, x.CountryCode, x.Status,
                db.ProviderCustomers.Where(p => p.BusinessCustomerId == x.Id && p.ProviderCode == ProviderCode.Blaaiz && !p.IsDeleted)
                    .Select(p => p.ProviderStatus).FirstOrDefault()))
            .PaginateAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
    }

    public async Task<BusinessCollectionCustomerDto> CreateCustomerAsync(
        Guid userId, CreatePortalCustomerRequestDto request, CancellationToken ct = default)
    {
        var actor = await RequireWrite(userId, ct);
        var reference = Required(request.ExternalReference, 150, "Customer reference");
        var name = Required(request.DisplayName, 200, "Customer name");
        var country = Required(request.CountryCode, 10, "Country").ToUpperInvariant();
        var email = Optional(request.Email, 255)?.ToLowerInvariant();
        var phone = Optional(request.PhoneNumber, 50);
        if (email is not null && !MailAddress.TryCreate(email, out _)) throw new InvalidOperationException("Enter a valid email address.");
        if (!await db.Countries.AsNoTracking().AnyAsync(x => x.Code == country && x.IsSupported, ct))
            throw new InvalidOperationException("Select a supported country from master data.");

        async Task<BusinessCollectionCustomerDto?> Replay()
        {
            var existing = await db.BusinessCustomers.AsNoTracking().FirstOrDefaultAsync(x =>
                x.BusinessProfileId == actor.BusinessProfileId && x.ExternalReference == reference && !x.IsDeleted, ct);
            if (existing is null) return null;
            if (existing.DisplayName != name || existing.Email != email || existing.PhoneNumber != phone || existing.CountryCode != country)
                throw new InvalidOperationException("This customer reference is already used by different customer details.");
            return await CustomerDto(existing, ct);
        }

        var replay = await Replay();
        if (replay is not null) return replay;
        var customer = new BusinessCustomer { BusinessProfileId = actor.BusinessProfileId, ExternalReference = reference,
            DisplayName = name, CountryCode = country, Email = email, PhoneNumber = phone, CreatedByUserId = userId };
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.BusinessCustomers.Add(customer);
            outbox.Stage(actor.BusinessProfileId, "customer.created", new { id = customer.Id, customer.ExternalReference,
                customer.DisplayName, customer.Email, customer.PhoneNumber, customer.CountryCode, status = customer.Status.ToString(), customer.CreatedAt });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return await Replay() ?? throw new InvalidOperationException("Customer creation conflicted with another request. Refresh and try again.");
        }
        return await CustomerDto(customer, ct);
    }

    public async Task<BusinessCollectionOverviewDto> GetAccountsAsync(Guid userId, Guid customerId, CancellationToken ct = default)
    {
        var actor = await access.EnsurePermissionAsync(userId, Read, ct);
        var customer = await Customer(actor.BusinessProfileId, customerId, ct);
        var verified = await BusinessVerified(actor.BusinessProfileId, ct);
        var providerCustomer = await CustomerDto(customer, ct);
        var registered = provisioners.FirstOrDefault(x => x.ProviderCode.Equals("Blaaiz", StringComparison.OrdinalIgnoreCase));
        var assets = await db.Assets.AsNoTracking().Where(x => x.IsSupported && x.DepositEnabled && x.Type == AssetType.Fiat)
            .OrderBy(x => x.Code).ToListAsync(ct);
        var eligible = assets.Where(x => registered?.Supports(customer.CountryCode, x.Code) == true).ToList();
        var defaults = await db.ProviderWalletConfigurations.AsNoTracking().Where(x => !x.IsDeleted && x.ProviderCode == "BLAAIZ" &&
            x.Environment == wallets.EnvironmentName && x.NetworkCode == "" && x.DefaultForCollection).ToListAsync(ct);
        var accounts = await db.CollectionAccounts.AsNoTracking().Include(x => x.FinancialAccount).Include(x => x.ProviderMappings)
            .Where(x => x.BusinessProfileId == actor.BusinessProfileId && x.BusinessCustomerId == customerId && !x.IsDeleted)
            .OrderBy(x => x.AssetCode).ToListAsync(ct);
        var ids = accounts.Select(x => x.Id).ToArray();
        var selections = await db.ProviderWalletSelections.AsNoTracking().Include(x => x.WalletConfiguration)
            .Where(x => x.OperationType == "CollectionAccount" && ids.Contains(x.OperationId)).ToListAsync(ct);
        var canManage = actor.HasPermission(Write);
        var rows = accounts.Select(account =>
        {
            var mapping = account.ProviderMappings.FirstOrDefault(x => !x.IsDeleted && x.ProviderCode.Equals("Blaaiz", StringComparison.OrdinalIgnoreCase));
            var selection = selections.SingleOrDefault(x => x.OperationId == account.Id);
            var wallet = selection is not null ? selection.WalletConfiguration : defaults.FirstOrDefault(x => x.AssetCode == account.AssetCode);
            var walletReady = WalletReady(wallet, account.AssetCode) && (selection is null ||
                selection.Purpose == ProviderWalletResolver.Collection && selection.ProviderWalletId == wallet!.ProviderWalletId);
            var requirements = new List<string>();
            if (!canManage) requirements.Add("Your role can view accounts. Ask a business administrator to make changes.");
            if (!verified) requirements.Add("Complete business verification before requesting bank details.");
            if (customer.Status != BusinessCustomerStatus.Active) requirements.Add("This customer is suspended or closed. Contact support.");
            if (!eligible.Any(x => x.Code == account.AssetCode)) requirements.Add("This currency is not currently available for collection accounts.");
            if (!options.Value.IsEnabled) requirements.Add("Bank account requests are temporarily unavailable. Contact support.");
            if (string.IsNullOrWhiteSpace(providerCustomer.ProviderStatus)) requirements.Add("Complete this customer's provider onboarding through your administrator.");
            else if (account.AssetCode is "EUR" or "GBP" && !providerCustomer.ProviderStatus.Equals("VERIFIED", StringComparison.OrdinalIgnoreCase))
                requirements.Add("The provider must verify this customer before an EUR/GBP account can be requested.");
            if (!walletReady) requirements.Add("The collection wallet needs administrator configuration or verification.");
            if (account.Status is CollectionAccountStatus.Suspended or CollectionAccountStatus.Closed || account.FinancialAccount.Status != FinancialAccountStatus.Active)
                requirements.Add("This account is restricted. Contact support.");
            if (mapping?.Status == ProviderAccountMappingStatus.Pending) requirements.Add("The provider is preparing bank details. Refresh to check the status.");
            if (mapping?.Status == ProviderAccountMappingStatus.Disabled) requirements.Add("An administrator has disabled this bank account mapping. Contact support.");
            var showDetails = mapping?.Status == ProviderAccountMappingStatus.Active && account.Status == CollectionAccountStatus.Active &&
                account.FinancialAccount.Status == FinancialAccountStatus.Active && customer.Status == BusinessCustomerStatus.Active &&
                verified && options.Value.IsEnabled && walletReady && eligible.Any(x => x.Code == account.AssetCode) &&
                (account.AssetCode is not ("EUR" or "GBP") || string.Equals(providerCustomer.ProviderStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase));
            return new BusinessCollectionAccountDto(account.Id, account.ExternalReference, account.AssetCode, account.Status,
                account.FinancialAccount.SettledBalance, account.FinancialAccount.AvailableBalance, account.FinancialAccount.HeldBalance,
                mapping?.Status, showDetails, requirements.Count == 0 && mapping?.Status != ProviderAccountMappingStatus.Active, requirements,
                showDetails ? mapping!.AccountNumber : null, showDetails ? mapping!.AccountName : null, showDetails ? mapping!.BankName : null,
                showDetails ? BlaaizVirtualAccountState.BankDetails(mapping!.MetadataJson) : null, mapping?.LastUpdatedAt ?? account.LastUpdatedAt);
        }).ToList();
        return new(providerCustomer, canManage, verified,
            eligible.Select(x => new BusinessCollectionAssetDto(x.Code, x.Name, WalletReady(defaults.FirstOrDefault(w => w.AssetCode == x.Code), x.Code))).ToList(), rows);
    }

    public async Task<BusinessCollectionOverviewDto> CreateAccountAsync(
        Guid userId, Guid customerId, CreateCollectionAccountRequestDto request, CancellationToken ct = default)
    {
        var actor = await RequireWrite(userId, ct);
        var customer = await Customer(actor.BusinessProfileId, customerId, ct);
        if (customer.Status != BusinessCustomerStatus.Active) throw new InvalidOperationException("The customer must be active to add an account.");
        var reference = Required(request.ExternalReference, 150, "Account reference");
        var currency = Required(request.AssetCode, 20, "Currency").ToUpperInvariant();
        var overview = await GetAccountsAsync(userId, customerId, ct);
        if (!overview.SupportedAssets.Any(x => x.Code == currency)) throw new InvalidOperationException("Select a supported collection currency from master data.");

        async Task<bool> Replay()
        {
            var existing = await db.CollectionAccounts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.BusinessProfileId == actor.BusinessProfileId && x.ExternalReference == reference && !x.IsDeleted, ct);
            if (existing is null) return false;
            if (existing.BusinessCustomerId != customerId || existing.AssetCode != currency)
                throw new InvalidOperationException("This account reference is already used by another customer or currency.");
            return true;
        }
        if (await Replay()) return overview;
        if (overview.Accounts.Any(x => x.AssetCode == currency)) throw new InvalidOperationException("This customer already has an account in the selected currency.");
        var financial = new FinancialAccount { OwnerType = FinancialAccountOwnerType.BusinessCustomer, OwnerId = customerId,
            AccountCode = $"BC-{customerId:N}-{currency}", AssetCode = currency, AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active, CreatedByUserId = userId };
        var account = new CollectionAccount { BusinessProfileId = actor.BusinessProfileId, BusinessCustomerId = customerId,
            FinancialAccount = financial, ExternalReference = reference, AssetCode = currency, Status = CollectionAccountStatus.Pending, CreatedByUserId = userId };
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.CollectionAccounts.Add(account);
            outbox.Stage(actor.BusinessProfileId, "account.created", new { id = account.Id, businessCustomerId = customerId,
                account.ExternalReference, account.AssetCode, financialAccountId = financial.Id, status = account.Status.ToString(), account.CreatedAt });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            if (!await Replay()) throw new InvalidOperationException("An account was added by another request. Refresh the customer accounts.");
        }
        return await GetAccountsAsync(userId, customerId, ct);
    }

    public async Task<BusinessCollectionOverviewDto> ProvisionAsync(Guid userId, Guid customerId, Guid accountId, CancellationToken ct = default)
    {
        var actor = await RequireWrite(userId, ct);
        var overview = await GetAccountsAsync(userId, customerId, ct);
        var account = overview.Accounts.SingleOrDefault(x => x.Id == accountId) ?? throw new InvalidOperationException("Collection account not found.");
        if (!account.CanRequestBankDetails) throw new InvalidOperationException(account.Requirements.FirstOrDefault() ?? "Bank details have already been requested. Refresh the account status.");
        var tracked = await db.CollectionAccounts.SingleAsync(x => x.Id == accountId && x.BusinessProfileId == actor.BusinessProfileId, ct);
        tracked.LastUpdatedByUserId = userId;
        tracked.LastUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await provisioning.ProvisionForBusinessAsync(actor.BusinessProfileId, customerId, accountId, new("Blaaiz"), ct);
        return await GetAccountsAsync(userId, customerId, ct);
    }

    private async Task<BusinessAccessContext> RequireWrite(Guid userId, CancellationToken ct)
    {
        var actor = await access.EnsurePermissionAsync(userId, Write, ct);
        if (!await BusinessVerified(actor.BusinessProfileId, ct)) throw new ForbiddenException("Complete business verification before managing customer accounts.");
        return actor;
    }
    private Task<bool> BusinessVerified(Guid id, CancellationToken ct) => db.BusinessProfiles.AsNoTracking()
        .AnyAsync(x => x.Id == id && !x.IsDeleted && x.KybStatus == KybStatus.Approved, ct);
    private async Task<BusinessCustomer> Customer(Guid businessId, Guid id, CancellationToken ct) =>
        await db.BusinessCustomers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.BusinessProfileId == businessId && !x.IsDeleted, ct)
        ?? throw new InvalidOperationException("Business customer not found.");
    private async Task<BusinessCollectionCustomerDto> CustomerDto(BusinessCustomer x, CancellationToken ct) =>
        new(x.Id, x.ExternalReference, x.DisplayName, x.Email, x.PhoneNumber, x.CountryCode, x.Status,
            await db.ProviderCustomers.AsNoTracking().Where(p => p.BusinessCustomerId == x.Id && p.ProviderCode == ProviderCode.Blaaiz && !p.IsDeleted)
                .Select(p => p.ProviderStatus).FirstOrDefaultAsync(ct));
    private bool WalletReady(ProviderWalletConfiguration? wallet, string currency) => wallet is not null && !wallet.IsDeleted &&
        wallet.IsActive && wallet.CollectionEnabled && wallet.ProviderCode == "BLAAIZ" && wallet.AssetCode == currency && wallet.NetworkCode == "" &&
        wallet.Environment == wallets.EnvironmentName && wallet.VerifiedAt is not null && wallet.VerifiedConnectionKey == wallets.ConnectionKey;
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    private static string Required(string? value, int length, string label) => Optional(value, length) ?? throw new InvalidOperationException($"{label} is required.");
    private static string? Optional(string? value, int length)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > length) throw new InvalidOperationException($"Value cannot exceed {length} characters.");
        return text;
    }
}
