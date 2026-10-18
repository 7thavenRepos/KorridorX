using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Dtos.Payments;
using KorridorX.Infrastructure;
using KorridorX.Models.Audit;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.PaymentChannels;

public sealed record PaymentChannelChoice(PaymentChannel Channel, ProviderWalletConfiguration Wallet,
    PaymentChannelCapabilityDto Capability);

public sealed class PaymentChannelService(AppDbContext db, IProviderWalletResolver wallets,
    IOptions<BlaaizOptions> options, ICollectionPaymentMethodPolicy legacy)
{
    public string EnvironmentName => wallets.EnvironmentName;
    public IReadOnlyList<PaymentChannelCapabilityDto> Capabilities => BlaaizChannelCapabilities.All;

    // Explicit handover begins on the first activation, not when drafting a rule.
    // It remains managed after pausing, so disabled methods cannot reappear via legacy fallback.
    public Task<bool> IsManagedAsync(string asset, PaymentChannelDirection direction, CancellationToken ct = default) =>
        db.PaymentChannels.AsNoTracking().AnyAsync(x => x.Environment == EnvironmentName &&
            x.AssetCode == asset && x.Direction == direction && x.HasBeenActivated, ct);

    public async Task<PagedResult<PaymentChannelDto>> ListAsync(int page, int pageSize, string? asset, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.PaymentChannels.AsNoTracking().Where(x => x.Environment == EnvironmentName && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(asset)) { var code = asset.Trim().ToUpperInvariant(); query = query.Where(x => x.AssetCode == code); }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.AssetCode).ThenBy(x => x.Direction).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var result = new List<PaymentChannelDto>();
        foreach (var row in rows) result.Add(await DescribeAsync(row, ct));
        return new PagedResult<PaymentChannelDto> { Items = result, Meta = new PageMeta { Page = page, PageSize = pageSize, TotalItems = count } };
    }

    public async Task<PaymentChannelDto> SaveAsync(Guid? id, Guid actor, SavePaymentChannelRequest request, CancellationToken ct)
    {
        var country = Required(request.CountryCode, 10, "Country").ToUpperInvariant();
        var asset = Required(request.AssetCode, 20, "Asset").ToUpperInvariant();
        var code = Required(request.CapabilityCode, 60, "Capability").ToLowerInvariant();
        var reason = Required(request.Reason, 500, "Reason");
        if (!Enum.IsDefined(request.Direction) || !Enum.IsDefined(request.Status)) throw new InvalidOperationException("Invalid channel direction or status.");
        var cap = BlaaizChannelCapabilities.Find(code, asset, request.Direction)
            ?? throw new InvalidOperationException("Select a capability supported by the implemented provider.");
        if (!request.IndividualEnabled && !request.BusinessEnabled) throw new InvalidOperationException("Select at least one customer audience.");
        if (request.IndividualEnabled && !cap.IndividualSupported || request.BusinessEnabled && !cap.BusinessSupported)
            throw new InvalidOperationException("This capability does not support the selected customer audience. Business card checkout and non-NGN business virtual accounts are not available.");
        if (request.MinimumAmount < 0 || request.MaximumAmount is <= 0 || request.MaximumAmount < request.MinimumAmount || request.SortOrder is < 0 or > 10000)
            throw new InvalidOperationException("Enter valid amount limits and a display order from 0 to 10000.");
        if (code == BlaaizChannelCapabilities.VirtualAccount && (request.MinimumAmount != 0 || request.MaximumAmount.HasValue))
            throw new InvalidOperationException("Static bank deposits are unsolicited; transaction limits cannot be enforced by this form. Leave limits empty/zero and retain compliance controls.");
        var configuredAsset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Code == asset, ct)
            ?? throw new InvalidOperationException("Select an asset from master data.");
        if (configuredAsset.Type != AssetType.Fiat) throw new InvalidOperationException("Use the separate digital-asset network configuration for crypto.");
        if (decimal.Round(request.MinimumAmount, configuredAsset.DecimalPlaces) != request.MinimumAmount ||
            request.MaximumAmount.HasValue && decimal.Round(request.MaximumAmount.Value, configuredAsset.DecimalPlaces) != request.MaximumAmount.Value)
            throw new InvalidOperationException("Amount limits exceed the asset's configured decimal precision.");
        if (country != "*" && !await db.Countries.AnyAsync(x => x.Code == country, ct)) throw new InvalidOperationException("Select a configured country.");
        var wallet = await db.ProviderWalletConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProviderWalletConfigurationId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Select a configured provider wallet.");
        if (wallet.ProviderCode != "BLAAIZ" || wallet.Environment != EnvironmentName || wallet.NetworkCode != "")
            throw new InvalidOperationException("Select a Blaaiz fiat wallet for this environment.");
        if (request.Direction == PaymentChannelDirection.Deposit && wallet.AssetCode != asset)
            throw new InvalidOperationException("The collection wallet must use the deposit asset.");
        var row = id.HasValue ? await db.PaymentChannels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Environment == EnvironmentName && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Channel not found.") : new PaymentChannel { CreatedByUserId = actor, Environment = EnvironmentName };
        var old = id.HasValue ? JsonSerializer.Serialize(row) : null;
        if (id.HasValue)
        {
            if (request.Revision != row.Revision) throw new InvalidOperationException("This channel changed. Refresh before saving.");
            if (row.ProviderWalletConfigurationId != wallet.Id || row.AssetCode != asset || row.CountryCode != country || row.CapabilityCode != code || row.Direction != request.Direction)
                throw new InvalidOperationException("Country, asset, capability and provider wallet identify a route and cannot be changed. Create a new channel and pause the old one.");
        }
        if (await db.PaymentChannels.AnyAsync(x => x.Environment == EnvironmentName && x.Id != row.Id && x.Direction == request.Direction &&
            x.AssetCode == asset && x.CountryCode == country && x.CapabilityCode == code && x.ProviderWalletConfigurationId == wallet.Id, ct))
            throw new InvalidOperationException("This route already exists. Configure the existing channel instead.");
        row.AssetCode = asset; row.CountryCode = country; row.Direction = request.Direction;
        row.CapabilityCode = code; row.ProviderCode = "BLAAIZ"; row.ProviderWalletConfigurationId = wallet.Id;
        row.DisplayName = Required(request.DisplayName, 120, "Display name"); row.Description = (request.Description ?? "").Trim();
        if (row.Description.Length > 500) throw new InvalidOperationException("Description is too long.");
        row.IndividualEnabled = request.IndividualEnabled; row.BusinessEnabled = request.BusinessEnabled;
        row.ProviderCapabilityConfirmed = request.ProviderCapabilityConfirmed; row.Status = request.Status;
        row.HasBeenActivated = row.HasBeenActivated || request.Status == PaymentChannelStatus.Active;
        row.MinimumAmount = request.MinimumAmount; row.MaximumAmount = request.MaximumAmount; row.SortOrder = request.SortOrder;
        row.Revision = Guid.NewGuid(); row.LastUpdatedAt = DateTime.UtcNow; row.LastUpdatedByUserId = actor;
        var description = await DescribeAsync(row, ct);
        if (row.Status == PaymentChannelStatus.Active && !description.Ready)
            throw new InvalidOperationException("Channel cannot be activated: " + string.Join(" ", description.BlockedReasons));
        if (!id.HasValue) db.PaymentChannels.Add(row);
        else
        {
            // Validation above operates on a detached copy. Rejected edits must
            // not leave changed entities for another SaveChanges in this scope.
            var tracked = db.ChangeTracker.Entries<PaymentChannel>().SingleOrDefault(x => x.Entity.Id == row.Id);
            if (tracked is not null) tracked.State = EntityState.Detached;
            db.PaymentChannels.Update(row);
            db.Entry(row).Property(x => x.Revision).OriginalValue = request.Revision!.Value;
        }
        db.AuditLogs.Add(new AuditLog { UserId = actor, Action = id.HasValue ? "PAYMENT_CHANNEL_UPDATED" : "PAYMENT_CHANNEL_CREATED",
            Category = "Payments", EntityName = nameof(PaymentChannel), EntityId = row.Id.ToString(), OldValuesJson = old,
            NewValuesJson = JsonSerializer.Serialize(row), MetadataJson = JsonSerializer.Serialize(new { reason }) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("This channel changed. Refresh before saving."); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new InvalidOperationException("This channel was just created by another administrator. Refresh the list."); }
        return description;
    }

    public async Task<PaymentChannelDto> SetStatusAsync(Guid id, Guid actor, SetPaymentChannelStatusRequest request, CancellationToken ct)
    {
        var row = await db.PaymentChannels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Environment == EnvironmentName && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Channel not found.");
        return await SaveAsync(id, actor, new(request.Revision, row.ProviderWalletConfigurationId, row.AssetCode, row.CountryCode,
            row.Direction, row.CapabilityCode, row.DisplayName, row.Description, row.IndividualEnabled, row.BusinessEnabled,
            row.ProviderCapabilityConfirmed, request.Status, row.SortOrder, row.MinimumAmount, row.MaximumAmount, request.Reason), ct);
    }

    public async Task<PaymentChannelDto> DescribeAsync(PaymentChannel row, CancellationToken ct)
    {
        var errors = new List<string>(); var warnings = new List<string>();
        var cap = BlaaizChannelCapabilities.Find(row.CapabilityCode, row.AssetCode, row.Direction);
        var wallet = await db.ProviderWalletConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.ProviderWalletConfigurationId, ct);
        if (cap is null || !cap.Implemented) errors.Add(cap?.BlockedReason ?? "Provider capability is not implemented.");
        if (cap is not null) warnings.Add(cap.Note);
        if (row.CapabilityCode == BlaaizChannelCapabilities.VirtualAccount && row.AssetCode == "USD" && IsProviderDevelopment)
            warnings.Add("Blaaiz development supports USD account creation only; test deposits do not settle. Do not send real funds.");
        if (!options.Value.IsEnabled) errors.Add("Blaaiz connection is disabled.");
        if (!row.ProviderCapabilityConfirmed) errors.Add("Confirm the provider has approved/enabled this payment capability for your account.");
        if (wallet is null || wallet.IsDeleted || !wallet.IsActive || wallet.ProviderCode != row.ProviderCode || wallet.Environment != EnvironmentName || wallet.NetworkCode != "")
            errors.Add("An active Blaaiz fiat provider wallet in this environment is required.");
        if (wallet is not null)
        {
            if (wallet.VerifiedAt is null || wallet.VerifiedConnectionKey != wallets.ConnectionKey) errors.Add("Verify the wallet for the current provider connection.");
            if (row.Direction == PaymentChannelDirection.Deposit && (!wallet.CollectionEnabled || wallet.AssetCode != row.AssetCode)) errors.Add("Enable collections on a wallet with the deposit asset.");
            if (row.Direction == PaymentChannelDirection.Withdrawal && !wallet.PayoutEnabled) errors.Add("Enable payouts on the funding wallet.");
            if (row.CapabilityCode == BlaaizChannelCapabilities.InteracRequest && !wallet.DefaultForCollection) errors.Add("Interac requests require the connection's default CAD collection wallet.");
        }
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Code == row.AssetCode, ct);
        if (asset is null || !asset.IsSupported || asset.Type != AssetType.Fiat ||
            (row.Direction == PaymentChannelDirection.Deposit ? !asset.DepositEnabled : !asset.WithdrawalEnabled)) errors.Add("Enable this asset and operation in master data.");
        if (asset is not null && (row.IndividualEnabled && !asset.IndividualEnabled || row.BusinessEnabled && !asset.BusinessEnabled)) errors.Add("Enable the selected asset audiences in master data.");
        if (cap is not null && (row.IndividualEnabled && !cap.IndividualSupported || row.BusinessEnabled && !cap.BusinessSupported)) errors.Add("Selected audience is not supported by the provider capability.");
        var mappings = db.CountryAssets.AsNoTracking().Where(x => x.AssetCode == row.AssetCode && x.Country.IsSupported);
        if (row.CountryCode != "*") mappings = mappings.Where(x => x.CountryCode == row.CountryCode);
        var any = row.Direction == PaymentChannelDirection.Deposit ? await mappings.AnyAsync(x => x.CanDeposit, ct)
            : await mappings.AnyAsync(x => x.CanWithdraw || x.CanReceive, ct);
        if (!any) errors.Add("Enable the country/asset mapping for this operation.");
        if (row.Direction == PaymentChannelDirection.Withdrawal && row.CountryCode != "*" &&
            (row.AssetCode == "NGN" && row.CountryCode != "NG" || row.AssetCode == "CAD" && row.CountryCode != "CA")) errors.Add("The enabled payout rail requires its supported destination country (NG/NGN or CA/CAD).");
        if (row.CapabilityCode == BlaaizChannelCapabilities.VirtualAccount && row.AssetCode is "GBP" or "EUR" && IsProviderDevelopment)
            errors.Add("Blaaiz GBP/EUR virtual-account creation is on hold in its development environment. Do not enable it there.");
        return new(row.Id, row.Revision, row.Environment, row.ProviderCode, row.ProviderWalletConfigurationId,
            wallet?.DisplayName ?? "Unavailable wallet", wallet?.AssetCode ?? "", row.AssetCode, row.CountryCode,
            row.Direction, row.CapabilityCode, row.DisplayName, row.Description, row.IndividualEnabled,
            row.BusinessEnabled, row.ProviderCapabilityConfirmed, row.Status, row.SortOrder, row.MinimumAmount,
            row.MaximumAmount, errors.Count == 0, errors.ToArray(), warnings.ToArray());
    }

    public bool IsProviderDevelopment => Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Host.Contains("api-dev", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<CollectionPaymentMethodDto>> DepositMethodsAsync(string country, string asset, bool business,
        bool walletFunding, decimal? amount, CancellationToken ct)
    {
        country = country.Trim().ToUpperInvariant(); asset = asset.Trim().ToUpperInvariant();
        if (!await IsManagedAsync(asset, PaymentChannelDirection.Deposit, ct))
            return legacy.GetSupportedMethods(country, asset).Where(x => !business || x == PaymentMethod.Interac)
                .Select(x => new CollectionPaymentMethodDto(x, x.ToString().ToLowerInvariant(), x == PaymentMethod.Interac ? "Interac e-Transfer" : "Debit / credit card") { IsLegacy = true }).ToArray();
        var choices = await EligibleAsync(country, asset, business, PaymentChannelDirection.Deposit, null, amount, ct);
        return choices.Where(x => walletFunding || x.Capability.Flow == "Collection").Select(ToMethod).ToArray();
    }

    public async Task<PaymentChannelChoice?> RequireDepositAsync(Guid? channelId, string country, string asset, bool business,
        bool walletFunding, PaymentMethod method, decimal amount, CancellationToken ct)
    {
        if (channelId is null && !await IsManagedAsync(asset, PaymentChannelDirection.Deposit, ct))
        {
            if (!legacy.IsSupported(country, asset, method) || business && method != PaymentMethod.Interac) throw new InvalidOperationException("This deposit method is not available.");
            return null; // Deliberate backwards compatibility only for a scope not yet managed in Admin.
        }
        var choices = (await EligibleAsync(country, asset, business, PaymentChannelDirection.Deposit, null, amount, ct))
            .Where(x => x.Capability.PaymentMethod == method && (walletFunding || x.Capability.Flow == "Collection") &&
                (!channelId.HasValue || x.Channel.Id == channelId)).ToArray();
        if (choices.Length != 1) throw new InvalidOperationException(choices.Length == 0
            ? "This deposit channel is unavailable. Refresh the payment methods and review the wallet/country requirements."
            : "Several channels use this payment method. Choose a specific channel in the updated application.");
        return choices[0];
    }

    public async Task<IReadOnlyList<PaymentChannelChoice>> EligibleAsync(string country, string asset, bool business,
        PaymentChannelDirection direction, string? fundingAsset, decimal? amount, CancellationToken ct)
    {
        if (!await db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == country && x.AssetCode == asset && x.Country.IsSupported &&
            x.Asset.IsSupported && (business ? x.Asset.BusinessEnabled : x.Asset.IndividualEnabled) &&
            (direction == PaymentChannelDirection.Deposit ? x.CanDeposit && x.Asset.DepositEnabled : (x.CanReceive || x.CanWithdraw) && x.Asset.WithdrawalEnabled), ct))
            return [];
        var rows = await db.PaymentChannels.AsNoTracking().Where(x => x.Environment == EnvironmentName && !x.IsDeleted &&
            x.Direction == direction && x.AssetCode == asset && (x.CountryCode == "*" || x.CountryCode == country) &&
            x.Status == PaymentChannelStatus.Active && (business ? x.BusinessEnabled : x.IndividualEnabled))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var result = new List<PaymentChannelChoice>();
        foreach (var row in rows)
        {
            var desc = await DescribeAsync(row, ct);
            if (!desc.Ready || fundingAsset is not null && desc.WalletAssetCode != fundingAsset ||
                amount.HasValue && !BlaaizChannelCapabilities.MatchesAmount(row.MinimumAmount, row.MaximumAmount, amount.Value)) continue;
            var cap = BlaaizChannelCapabilities.Find(row.CapabilityCode, row.AssetCode, row.Direction)!;
            if (direction == PaymentChannelDirection.Withdrawal && (asset == "NGN" && country != "NG" || asset == "CAD" && country != "CA")) continue;
            var wallet = await db.ProviderWalletConfigurations.AsNoTracking().SingleAsync(x => x.Id == row.ProviderWalletConfigurationId, ct);
            result.Add(new(row, wallet, cap));
        }
        return result;
    }

    public PaymentChannelSelection Record(PaymentChannelChoice choice, string type, Guid operationId,
        string country, bool business, Guid ownerId, Guid? walletId, decimal? amount, string? idempotencyKey = null)
    {
        var selection = new PaymentChannelSelection { OperationType = type, OperationId = operationId,
            ChannelId = choice.Channel.Id, ChannelRevision = choice.Channel.Revision,
            ProviderWalletConfigurationId = choice.Wallet.Id, ProviderWalletId = choice.Wallet.ProviderWalletId,
            ProviderCode = choice.Wallet.ProviderCode, Environment = choice.Wallet.Environment,
            CapabilityCode = choice.Capability.Code, AssetCode = choice.Channel.AssetCode, CountryCode = country,
            Business = business, PaymentMethod = choice.Capability.PaymentMethod, OwnerId = ownerId,
            FinancialAccountId = walletId, Amount = amount, IdempotencyKey = idempotencyKey };
        db.PaymentChannelSelections.Add(selection); return selection;
    }

    public async Task ValidateInitiationAsync(string type, Guid operationId, CancellationToken ct)
    {
        var selection = await db.PaymentChannelSelections.AsNoTracking().SingleOrDefaultAsync(x => x.OperationType == type && x.OperationId == operationId, ct);
        if (selection is null) return; // A pre-existing legacy operation retains its existing reconciliation/route behavior.
        var row = await db.PaymentChannels.AsNoTracking().SingleAsync(x => x.Id == selection.ChannelId, ct);
        var state = await DescribeAsync(row, ct);
        if (row.IsDeleted || row.Status != PaymentChannelStatus.Active || !state.Ready ||
            (selection.Business ? !row.BusinessEnabled : !row.IndividualEnabled))
            throw new InvalidOperationException("The selected channel is paused or unavailable. The saved payment has not been rerouted; contact support or resume after configuration is restored.");
        if (!BlaaizChannelCapabilities.MatchesCountry(row.CountryCode, selection.CountryCode) ||
            selection.Amount.HasValue && !BlaaizChannelCapabilities.MatchesAmount(row.MinimumAmount, row.MaximumAmount, selection.Amount.Value) ||
            !await db.CountryAssets.AsNoTracking().AnyAsync(x => x.CountryCode == selection.CountryCode && x.AssetCode == row.AssetCode &&
                x.Country.IsSupported && (row.Direction == PaymentChannelDirection.Deposit ? x.CanDeposit : x.CanReceive || x.CanWithdraw), ct))
            throw new InvalidOperationException("The country mapping or amount limits no longer permit initiation. This payment has not been rerouted.");
        if (row.ProviderWalletConfigurationId != selection.ProviderWalletConfigurationId || row.CapabilityCode != selection.CapabilityCode)
            throw new InvalidOperationException("The saved channel route does not match its immutable selection.");
    }

    public async Task<PaymentChannelChoice?> ResolvePayoutAsync(Transfer transfer, Payout? payout, CancellationToken ct)
    {
        if (payout?.PaymentChannelId is not null)
        {
            await ValidateInitiationAsync("Payout", payout.Id, ct);
            return null; // Use the existing snapshot and PaymentMethod; wallet resolver enforces the saved route.
        }
        if (payout is not null || !await IsManagedAsync(transfer.DestinationCurrencyCode, PaymentChannelDirection.Withdrawal, ct)) return null;
        var candidates = await EligibleAsync(transfer.DestinationCountryCode, transfer.DestinationCurrencyCode,
            transfer.BusinessProfileId.HasValue, PaymentChannelDirection.Withdrawal, transfer.SourceCurrencyCode, transfer.DestinationAmount, ct);
        return candidates.FirstOrDefault() ?? throw new InvalidOperationException("No active payout channel matches this destination, audience, funding asset and amount. Review Payment channels in Admin.");
    }

    public static CollectionPaymentMethodDto ToMethod(PaymentChannelChoice choice) => new(choice.Capability.PaymentMethod,
        choice.Capability.Code, choice.Channel.DisplayName) { ChannelId = choice.Channel.Id, Flow = choice.Capability.Flow,
        Description = choice.Channel.Description, MinimumAmount = choice.Channel.MinimumAmount, MaximumAmount = choice.Channel.MaximumAmount };
    public static string Required(string? value, int max, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
        ? throw new InvalidOperationException($"{name} is required and must be at most {max} characters.") : value.Trim();
}
