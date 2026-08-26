using System.Data;
using KorridorX.Data;
using KorridorX.Dtos.BusinessDigitalAssets;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.Customers;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.DigitalAssets;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.BusinessDigitalAssets;

public sealed class BusinessDigitalAssetService : IBusinessDigitalAssetService
{
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _access;
    private readonly IDigitalAssetProviderRegistry _providers;
    private readonly IFinancialReservationService _reservations;
    private readonly IOutboundFundsRestrictionService _outboundRestrictions;
    private readonly IDigitalAssetComplianceGate _compliance;
    private readonly DigitalAssetComplianceOptions _complianceOptions;

    public BusinessDigitalAssetService(
        AppDbContext db,
        IBusinessAccessService access,
        IDigitalAssetProviderRegistry providers,
        IFinancialReservationService reservations,
        IOutboundFundsRestrictionService outboundRestrictions,
        IDigitalAssetComplianceGate compliance,
        IOptions<DigitalAssetComplianceOptions> complianceOptions)
    {
        _db = db;
        _access = access;
        _providers = providers;
        _reservations = reservations;
        _outboundRestrictions = outboundRestrictions;
        _compliance = compliance;
        _complianceOptions = complianceOptions.Value;
    }

    public async Task<BusinessDigitalAssetWorkspaceDto> GetWorkspaceAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var (_, profile) = await GetContextAsync(
            userId,
            BusinessPermission.ViewDigitalAssets,
            requireApproved: false,
            ct);

        var balances = await (
            from account in _db.FinancialAccounts.AsNoTracking()
            join asset in _db.Assets.AsNoTracking()
                on account.AssetCode equals asset.Code
            where account.OwnerType == FinancialAccountOwnerType.Business &&
                  account.OwnerId == profile.Id &&
                  account.AccountType == FinancialAccountType.Customer &&
                  asset.Type == AssetType.Crypto &&
                  !account.IsDeleted
            orderby account.AssetCode
            select new BusinessDigitalAssetBalanceDto(
                account.Id,
                account.AssetCode,
                asset.Name,
                asset.Type,
                account.Status,
                account.SettledBalance,
                account.AvailableBalance,
                account.HeldBalance))
            .ToListAsync(ct);

        var countryCode = TradingCountry(profile);

        var countryAssets = await _db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Asset)
            .Where(x =>
                x.CountryCode == countryCode &&
                x.Asset.Type == AssetType.Crypto &&
                x.Asset.IsSupported &&
                (x.CanDeposit || x.CanWithdraw))
            .ToListAsync(ct);

        var assetCodes = countryAssets
            .Select(x => x.AssetCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var networks = await _db.AssetNetworks
            .AsNoTracking()
            .Include(x => x.Asset)
            .Where(x =>
                assetCodes.Contains(x.AssetCode) &&
                x.Status == AssetNetworkStatus.Active)
            .OrderBy(x => x.AssetCode)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        var providerConfigs = await _db.DigitalAssetProviderConfigurations
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(ct);

        var result = new List<BusinessDigitalAssetNetworkDto>();

        foreach (var network in networks)
        {
            var mapping = countryAssets.First(x =>
                string.Equals(
                    x.AssetCode,
                    network.AssetCode,
                    StringComparison.OrdinalIgnoreCase));

            var canDeposit =
                mapping.CanDeposit &&
                network.Asset.DepositEnabled &&
                network.DepositEnabled;

            var canWithdraw =
                mapping.CanWithdraw &&
                network.Asset.WithdrawalEnabled &&
                network.WithdrawalEnabled;

            var options = new List<BusinessDigitalAssetProviderOptionDto>();

            foreach (var config in providerConfigs)
            {
                IDigitalAssetProvider provider;
                try
                {
                    provider = _providers.GetRequired(config.ProviderCode);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (!provider.Supports(network.AssetCode, network.NetworkCode))
                    continue;

                var supportsIntent = provider is IDigitalAssetCollectionProvider;

                options.Add(new BusinessDigitalAssetProviderOptionDto(
                    provider.ProviderCode,
                    config.DisplayName,
                    SupportsDepositAddress: canDeposit && !supportsIntent,
                    SupportsDepositIntent: canDeposit && supportsIntent,
                    SupportsWithdrawal: canWithdraw));
            }

            result.Add(new BusinessDigitalAssetNetworkDto(
                network.Id,
                network.AssetCode,
                network.Asset.Name,
                network.NetworkCode,
                network.Name,
                network.RequiredConfirmations,
                network.MinimumDeposit,
                network.MinimumWithdrawal,
                network.WithdrawalFee,
                canDeposit,
                canWithdraw,
                options));
        }

        return new BusinessDigitalAssetWorkspaceDto(
            balances,
            result,
            _complianceOptions.TravelRuleThreshold);
    }

    public async Task<IReadOnlyList<DigitalAssetDepositAddressDto>> GetDepositAddressesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var (access, _) = await GetContextAsync(
            userId,
            BusinessPermission.ViewDigitalAssets,
            requireApproved: false,
            ct);

        var rows = await _db.DigitalAssetDepositAddresses
            .AsNoTracking()
            .Include(x => x.AssetNetwork)
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToDepositAddressDto).ToList();
    }

    public async Task<DigitalAssetDepositAddressDto> CreateDepositAddressAsync(
        Guid userId,
        CreateDigitalAssetDepositAddressRequestDto request,
        CancellationToken ct = default)
    {
        var (access, profile) = await GetContextAsync(
            userId,
            BusinessPermission.ManageDigitalAssets,
            requireApproved: true,
            ct);

        var account = await GetBusinessAccountAsync(
            access.BusinessProfileId,
            request.FinancialAccountId,
            ct);

        var network = await GetNetworkAsync(
            request.AssetNetworkId,
            account.AssetCode,
            deposit: true,
            ct);

        await EnsureCountryAssetPermissionAsync(
            profile,
            account.AssetCode,
            deposit: true,
            ct);

        var provider = _providers.GetRequired(
            request.ProviderCode,
            account.AssetCode,
            network.NetworkCode);

        if (provider is IDigitalAssetCollectionProvider)
        {
            throw new InvalidOperationException(
                $"Digital-asset provider '{provider.ProviderCode}' uses amount-specific deposit intents. Use the deposit-intents endpoint.");
        }

        var existing = await _db.DigitalAssetDepositAddresses
            .AsNoTracking()
            .Include(x => x.AssetNetwork)
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                x.FinancialAccountId == account.Id &&
                x.AssetNetworkId == network.Id &&
                x.ProviderCode == provider.ProviderCode &&
                x.Status == DigitalAssetAddressStatus.Active &&
                !x.IsDeleted,
                ct);

        if (existing is not null)
            return ToDepositAddressDto(existing);

        var result = await provider.CreateDepositAddressAsync(
            new DigitalAssetDepositAddressRequest(
                access.BusinessProfileId,
                null,
                account.Id,
                account.AssetCode,
                network.NetworkCode),
            ct);

        var address = new DigitalAssetDepositAddress
        {
            BusinessProfileId = access.BusinessProfileId,
            BusinessCustomerId = null,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            AssetNetworkId = network.Id,
            AssetNetwork = network,
            ProviderCode = provider.ProviderCode,
            ProviderAddressId = Clean(result.ProviderAddressId, 150),
            Address = Required(result.Address, 300, "Deposit address"),
            DestinationTag = Clean(result.DestinationTag, 150),
            Status = DigitalAssetAddressStatus.Active
        };

        _db.DigitalAssetDepositAddresses.Add(address);
        await _db.SaveChangesAsync(ct);

        return ToDepositAddressDto(address);
    }

    public async Task<IReadOnlyList<DigitalAssetDepositIntentDto>> GetDepositIntentsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var (access, _) = await GetContextAsync(
            userId,
            BusinessPermission.ViewDigitalAssets,
            requireApproved: false,
            ct);

        var rows = await _db.DigitalAssetDepositIntents
            .AsNoTracking()
            .Include(x => x.AssetNetwork)
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToDepositIntentDto).ToList();
    }

    public async Task<DigitalAssetDepositIntentDto> CreateDepositIntentAsync(
        Guid userId,
        CreateDigitalAssetDepositIntentRequestDto request,
        CancellationToken ct = default)
    {
        var (access, profile) = await GetContextAsync(
            userId,
            BusinessPermission.ManageDigitalAssets,
            requireApproved: true,
            ct);

        if (request.Amount <= 0m)
            throw new InvalidOperationException(
                "Deposit amount must be greater than zero.");

        var account = await GetBusinessAccountAsync(
            access.BusinessProfileId,
            request.FinancialAccountId,
            ct);

        var network = await GetNetworkAsync(
            request.AssetNetworkId,
            account.AssetCode,
            deposit: true,
            ct);

        await EnsureCountryAssetPermissionAsync(
            profile,
            account.AssetCode,
            deposit: true,
            ct);

        var provider = _providers.GetRequired(
            request.ProviderCode,
            account.AssetCode,
            network.NetworkCode);

        if (provider is not IDigitalAssetCollectionProvider collections)
        {
            throw new InvalidOperationException(
                $"Digital-asset provider '{provider.ProviderCode}' does not support amount-specific deposit intents.");
        }

        if (request.Amount < network.MinimumDeposit)
        {
            throw new InvalidOperationException(
                $"Minimum deposit is {network.MinimumDeposit} {account.AssetCode}.");
        }

        var intent = new DigitalAssetDepositIntent
        {
            BusinessProfileId = access.BusinessProfileId,
            BusinessCustomerId = null,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            AssetNetworkId = network.Id,
            AssetNetwork = network,
            ProviderCode = provider.ProviderCode,
            AssetCode = account.AssetCode,
            NetworkCode = network.NetworkCode,
            Amount = request.Amount,
            Status = CollectionStatus.Pending
        };

        _db.DigitalAssetDepositIntents.Add(intent);
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await collections.CreateCollectionIntentAsync(
                new DigitalAssetCollectionIntentRequest(
                    intent.Id,
                    access.BusinessProfileId,
                    null,
                    account.Id,
                    account.AssetCode,
                    network.NetworkCode,
                    request.Amount),
                ct);

            intent.ProviderWalletId = Clean(result.ProviderWalletId, 150);
            intent.ProviderCollectionId = Required(
                result.ProviderCollectionId,
                200,
                "Provider collection ID");
            intent.ProviderReference = Clean(result.ProviderReference, 200);
            intent.Address = Required(
                result.Address,
                300,
                "Deposit address");
            intent.Status = CollectionStatus.Initiated;
            intent.ProviderExpiresAt = result.ExpiresAt;
            intent.InitiatedAt = DateTime.UtcNow;
            intent.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDepositIntentDto(intent);
        }
        catch (Exception ex)
        {
            intent.Status = CollectionStatus.Failed;
            intent.FailedAt = DateTime.UtcNow;
            intent.FailureReason = Clean(ex.Message, 2000);
            intent.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<DigitalAssetWithdrawalDestinationDto>> GetWithdrawalDestinationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var (access, _) = await GetContextAsync(
            userId,
            BusinessPermission.ViewDigitalAssets,
            requireApproved: false,
            ct);

        var rows = await _db.DigitalAssetWithdrawalDestinations
            .AsNoTracking()
            .Include(x => x.AssetNetwork)
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToDestinationDto).ToList();
    }

    public async Task<DigitalAssetWithdrawalDestinationDto> CreateWithdrawalDestinationAsync(
        Guid userId,
        CreateDigitalAssetWithdrawalDestinationRequestDto request,
        CancellationToken ct = default)
    {
        var (access, profile) = await GetContextAsync(
            userId,
            BusinessPermission.ManageDigitalAssets,
            requireApproved: true,
            ct);

        var network = await _db.AssetNetworks
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == request.AssetNetworkId, ct)
            ?? throw new InvalidOperationException("Asset network not found.");

        ValidateNetwork(network, network.AssetCode, deposit: false);

        await EnsureCountryAssetPermissionAsync(
            profile,
            network.AssetCode,
            deposit: false,
            ct);

        var destination = new DigitalAssetWithdrawalDestination
        {
            BusinessProfileId = access.BusinessProfileId,
            BusinessCustomerId = null,
            AssetNetworkId = network.Id,
            AssetNetwork = network,
            AssetCode = network.AssetCode,
            Address = Required(
                request.Address,
                300,
                "Withdrawal address"),
            DestinationTag = Clean(request.DestinationTag, 150),
            Label = Clean(request.Label, 150),
            Status = DigitalAssetDestinationStatus.Active
        };

        var existing = await _db.DigitalAssetWithdrawalDestinations
            .AsNoTracking()
            .Include(x => x.AssetNetwork)
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                x.AssetNetworkId == destination.AssetNetworkId &&
                x.Address == destination.Address &&
                x.DestinationTag == destination.DestinationTag &&
                !x.IsDeleted,
                ct);

        if (existing is not null)
            return ToDestinationDto(existing);

        _db.DigitalAssetWithdrawalDestinations.Add(destination);
        await _db.SaveChangesAsync(ct);

        return ToDestinationDto(destination);
    }

    public async Task<IReadOnlyList<DigitalAssetWithdrawalDto>> GetWithdrawalsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var (access, _) = await GetContextAsync(
            userId,
            BusinessPermission.ViewDigitalAssets,
            requireApproved: false,
            ct);

        var ids = await _db.DigitalAssetWithdrawals
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var result = new List<DigitalAssetWithdrawalDto>();
        foreach (var id in ids)
            result.Add(await ToWithdrawalDtoAsync(id, ct));

        return result;
    }

    public async Task<DigitalAssetWithdrawalDto> CreateWithdrawalAsync(
        Guid userId,
        CreateDigitalAssetWithdrawalRequestDto request,
        CancellationToken ct = default)
    {
        var (access, profile) = await GetContextAsync(
            userId,
            BusinessPermission.ManageDigitalAssets,
            requireApproved: true,
            ct);

        if (request.Amount <= 0m)
            throw new InvalidOperationException(
                "Withdrawal amount must be greater than zero.");

        var account = await GetBusinessAccountAsync(
            access.BusinessProfileId,
            request.FinancialAccountId,
            ct);

        var destination = await _db.DigitalAssetWithdrawalDestinations
            .Include(x => x.AssetNetwork)
            .ThenInclude(x => x.Asset)
            .FirstOrDefaultAsync(x =>
                x.Id == request.DestinationId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Digital-asset withdrawal destination not found.");

        if (destination.Status != DigitalAssetDestinationStatus.Active)
        {
            throw new InvalidOperationException(
                "Digital-asset withdrawal destination is not active.");
        }

        ValidateNetwork(
            destination.AssetNetwork,
            account.AssetCode,
            deposit: false);

        await EnsureCountryAssetPermissionAsync(
            profile,
            account.AssetCode,
            deposit: false,
            ct);

        if (request.Amount < destination.AssetNetwork.MinimumWithdrawal)
        {
            throw new InvalidOperationException(
                $"Minimum withdrawal is {destination.AssetNetwork.MinimumWithdrawal} {account.AssetCode}.");
        }

        var fee = destination.AssetNetwork.WithdrawalFee;
        var totalDebit = request.Amount + fee;

        await _compliance.EnsureWithdrawalAllowedAsync(
            access.BusinessProfileId,
            null,
            account.AssetCode,
            destination.AssetNetwork.NetworkCode,
            request.Amount,
            destination.Address,
            ct);

        var provider = _providers.GetRequired(
            request.ProviderCode,
            account.AssetCode,
            destination.AssetNetwork.NetworkCode);

        await using var tx = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct);

        var payout = new Payout
        {
            Purpose = PaymentOperationPurpose.Withdrawal,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            RelatedEntityType = nameof(DigitalAssetWithdrawal),
            ContextEntityType = nameof(BusinessProfile),
            ContextEntityId = access.BusinessProfileId,
            Reference = Clean(request.ExternalReference, 80)
                ?? $"DAX-BIZ-WD-{Guid.NewGuid():N}",
            CurrencyCode = account.AssetCode,
            Amount = request.Amount,
            PaymentMethod = PaymentMethod.DigitalAsset,
            Status = PayoutStatus.Pending,
            ProviderCode = provider.ProviderCode
        };

        var withdrawal = new DigitalAssetWithdrawal
        {
            BusinessProfileId = access.BusinessProfileId,
            BusinessCustomerId = null,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            AssetNetworkId = destination.AssetNetworkId,
            AssetNetwork = destination.AssetNetwork,
            DestinationId = destination.Id,
            Destination = destination,
            PayoutId = payout.Id,
            Payout = payout,
            ProviderCode = provider.ProviderCode,
            AssetCode = account.AssetCode,
            Amount = request.Amount,
            NetworkFee = fee,
            TotalDebitAmount = totalDebit
        };

        payout.RelatedEntityId = withdrawal.Id;

        _db.Payouts.Add(payout);
        _db.DigitalAssetWithdrawals.Add(withdrawal);

        var travelRuleRequired =
            _complianceOptions.TravelRuleThreshold > 0m &&
            request.Amount >= _complianceOptions.TravelRuleThreshold;

        if (travelRuleRequired && request.TravelRule is null)
        {
            throw new InvalidOperationException(
                "Travel Rule information is required for this digital-asset withdrawal.");
        }

        var travelRule = new DigitalAssetTravelRuleRecord
        {
            BusinessProfileId = access.BusinessProfileId,
            BusinessCustomerId = null,
            DigitalAssetWithdrawalId = withdrawal.Id,
            DigitalAssetWithdrawal = withdrawal,
            Status = travelRuleRequired
                ? DigitalAssetTravelRuleStatus.Ready
                : DigitalAssetTravelRuleStatus.NotRequired,
            ThresholdAmount = _complianceOptions.TravelRuleThreshold,
            AssetCode = account.AssetCode,
            NetworkCode = destination.AssetNetwork.NetworkCode,
            OriginatorVasp = Clean(
                request.TravelRule?.OriginatorVasp,
                200),
            BeneficiaryVasp = Clean(
                request.TravelRule?.BeneficiaryVasp,
                200),
            BeneficiaryName = Clean(
                request.TravelRule?.BeneficiaryName,
                200),
            PayloadJson = request.TravelRule?.PayloadJson
        };

        _db.DigitalAssetTravelRuleRecords.Add(travelRule);

        var reservation = await _reservations.ReserveAsync(
            account.Id,
            FinancialReservationType.Withdrawal,
            nameof(DigitalAssetWithdrawal),
            withdrawal.Id,
            totalDebit,
            null,
            nameof(Payout),
            payout.Id,
            ct);

        withdrawal.ReservationId = reservation.Id;
        withdrawal.Reservation = reservation;

        await _db.SaveChangesAsync(ct);

        try
        {
            // Canonical final outbound restriction gate. Nothing is persisted
            // between this check and provider dispatch.
            await _outboundRestrictions.EnsureBusinessOutboundAllowedAsync(
                access.BusinessProfileId,
                "business_digital_asset_withdrawal_provider_dispatch",
                ct);

            var submission = await provider.SubmitWithdrawalAsync(
                new DigitalAssetWithdrawalSubmissionRequest(
                    withdrawal.Id,
                    account.AssetCode,
                    destination.AssetNetwork.NetworkCode,
                    destination.Address,
                    destination.DestinationTag,
                    request.Amount,
                    payout.Reference),
                ct);

            var providerTransactionId = Required(
                submission.ProviderTransactionId,
                200,
                "Provider transaction ID");

            var networkTx = new DigitalAssetNetworkTransaction
            {
                ProviderCode = provider.ProviderCode,
                ProviderTransactionId = providerTransactionId,
                ProviderReference = Clean(
                    submission.ProviderReference,
                    200),
                AssetNetworkId = destination.AssetNetworkId,
                AssetNetwork = destination.AssetNetwork,
                AssetCode = account.AssetCode,
                Direction = DigitalAssetTransactionDirection.Outbound,
                Status = DigitalAssetTransactionStatus.Confirming,
                TransactionHash = Clean(
                    submission.TransactionHash,
                    300),
                ToAddress = destination.Address,
                DestinationTag = destination.DestinationTag,
                Amount = request.Amount,
                NetworkFee = fee,
                RequiredConfirmations =
                    destination.AssetNetwork.RequiredConfirmations,
                PayoutId = payout.Id,
                Payout = payout,
                ObservedAt = DateTime.UtcNow
            };

            _db.DigitalAssetNetworkTransactions.Add(networkTx);

            payout.ProviderPayoutId = providerTransactionId;
            payout.ProviderReference = networkTx.ProviderReference;
            payout.Status = PayoutStatus.Processing;
            payout.InitiatedAt = DateTime.UtcNow;

            withdrawal.Status = DigitalAssetWithdrawalStatus.Submitted;
            withdrawal.SubmittedAt = DateTime.UtcNow;
            withdrawal.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return await ToWithdrawalDtoAsync(withdrawal.Id, ct);
        }
        catch (Exception ex)
        {
            if (
                reservation.Status == FinancialReservationStatus.Active &&
                reservation.RemainingAmount > 0m)
            {
                await _reservations.ReleaseAsync(
                    reservation.Id,
                    reservation.RemainingAmount,
                    "Business digital-asset provider submission failed.",
                    null,
                    ct);
            }

            payout.Status = PayoutStatus.Failed;
            payout.FailedAt = DateTime.UtcNow;
            payout.FailureReason = Clean(ex.Message, 1000);

            withdrawal.Status = DigitalAssetWithdrawalStatus.Failed;
            withdrawal.FailedAt = DateTime.UtcNow;
            withdrawal.FailureReason = Clean(ex.Message, 1000);
            withdrawal.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<BusinessDigitalAssetActivityDto>> GetActivityAsync(
        Guid userId,
        int take = 100,
        CancellationToken ct = default)
    {
        var (access, _) = await GetContextAsync(
            userId,
            BusinessPermission.ViewDigitalAssets,
            requireApproved: false,
            ct);

        take = Math.Clamp(take, 1, 200);
        var contextType = nameof(BusinessProfile);

        var collections = await _db.Collections
            .AsNoTracking()
            .Where(x =>
                x.ContextEntityType == contextType &&
                x.ContextEntityId == access.BusinessProfileId &&
                x.PaymentMethod == PaymentMethod.DigitalAsset &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        var payouts = await _db.Payouts
            .AsNoTracking()
            .Where(x =>
                x.ContextEntityType == contextType &&
                x.ContextEntityId == access.BusinessProfileId &&
                x.PaymentMethod == PaymentMethod.DigitalAsset &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        var pendingIntents = await _db.DigitalAssetDepositIntents
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.BusinessCustomerId == null &&
                x.CollectionId == null &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        var activity = new List<BusinessDigitalAssetActivityDto>();

        activity.AddRange(collections.Select(x =>
            new BusinessDigitalAssetActivityDto(
                x.Id,
                "Deposit",
                x.Reference,
                x.CurrencyCode,
                null,
                x.Amount,
                x.Status.ToString(),
                x.CreatedAt,
                x.ConfirmedAt)));

        activity.AddRange(payouts.Select(x =>
            new BusinessDigitalAssetActivityDto(
                x.Id,
                "Withdrawal",
                x.Reference,
                x.CurrencyCode,
                null,
                x.Amount,
                x.Status.ToString(),
                x.CreatedAt,
                x.CompletedAt)));

        activity.AddRange(pendingIntents.Select(x =>
            new BusinessDigitalAssetActivityDto(
                x.Id,
                "Deposit intent",
                x.ProviderCollectionId ?? x.Id.ToString("N"),
                x.AssetCode,
                x.NetworkCode,
                x.Amount,
                x.Status.ToString(),
                x.CreatedAt,
                x.CompletedAt)));

        return activity
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .ToList();
    }

    private async Task<(BusinessAccessContext Access, BusinessProfile Profile)> GetContextAsync(
        Guid userId,
        BusinessPermission permission,
        bool requireApproved,
        CancellationToken ct)
    {
        var access = await _access.EnsurePermissionAsync(
            userId,
            permission,
            ct);

        var profile = await _db.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Business profile not found.");

        if (requireApproved && profile.KybStatus != KybStatus.Approved)
        {
            throw new InvalidOperationException(
                "Business digital-asset activity requires an approved KYB profile.");
        }

        return (access, profile);
    }

    private async Task<FinancialAccount> GetBusinessAccountAsync(
        Guid businessProfileId,
        Guid financialAccountId,
        CancellationToken ct)
    {
        var account = await _db.FinancialAccounts
            .FirstOrDefaultAsync(x =>
                x.Id == financialAccountId &&
                x.OwnerType == FinancialAccountOwnerType.Business &&
                x.OwnerId == businessProfileId &&
                x.AccountType == FinancialAccountType.Customer &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Business Financial Account not found.");

        if (account.Status != FinancialAccountStatus.Active)
        {
            throw new InvalidOperationException(
                "Financial Account must be active.");
        }

        return account;
    }

    private async Task<KorridorX.Models.Lookups.AssetNetwork> GetNetworkAsync(
        Guid assetNetworkId,
        string assetCode,
        bool deposit,
        CancellationToken ct)
    {
        var network = await _db.AssetNetworks
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == assetNetworkId, ct)
            ?? throw new InvalidOperationException(
                "Asset network not found.");

        ValidateNetwork(network, assetCode, deposit);
        return network;
    }

    private static void ValidateNetwork(
        KorridorX.Models.Lookups.AssetNetwork network,
        string assetCode,
        bool deposit)
    {
        if (!string.Equals(
                network.AssetCode,
                assetCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Asset network does not match the Financial Account asset.");
        }

        if (
            network.Asset.Type != AssetType.Crypto ||
            !network.Asset.IsSupported)
        {
            throw new InvalidOperationException(
                "Asset is not an enabled digital asset.");
        }

        if (network.Status != AssetNetworkStatus.Active)
        {
            throw new InvalidOperationException(
                "Asset network is not active.");
        }

        if (
            deposit &&
            (!network.Asset.DepositEnabled || !network.DepositEnabled))
        {
            throw new InvalidOperationException(
                "Digital-asset deposits are disabled for this network.");
        }

        if (
            !deposit &&
            (!network.Asset.WithdrawalEnabled ||
             !network.WithdrawalEnabled))
        {
            throw new InvalidOperationException(
                "Digital-asset withdrawals are disabled for this network.");
        }
    }

    private async Task EnsureCountryAssetPermissionAsync(
        BusinessProfile profile,
        string assetCode,
        bool deposit,
        CancellationToken ct)
    {
        var countryCode = TradingCountry(profile);
        var normalizedAsset = Required(
            assetCode,
            20,
            "Asset code").ToUpperInvariant();

        var mapping = await _db.CountryAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.CountryCode == countryCode &&
                x.AssetCode == normalizedAsset,
                ct);

        if (mapping is null)
        {
            throw new InvalidOperationException(
                $"Digital asset {normalizedAsset} is not enabled for the business in {countryCode}.");
        }

        if (deposit && !mapping.CanDeposit)
        {
            throw new InvalidOperationException(
                $"Digital-asset deposits are not enabled for {normalizedAsset} in {countryCode}.");
        }

        if (!deposit && !mapping.CanWithdraw)
        {
            throw new InvalidOperationException(
                $"Digital-asset withdrawals are not enabled for {normalizedAsset} in {countryCode}.");
        }
    }

    private async Task<DigitalAssetWithdrawalDto> ToWithdrawalDtoAsync(
        Guid id,
        CancellationToken ct)
    {
        var row = await _db.DigitalAssetWithdrawals
            .AsNoTracking()
            .Include(x => x.AssetNetwork)
            .FirstAsync(x => x.Id == id, ct);

        var networkTx = await _db.DigitalAssetNetworkTransactions
            .AsNoTracking()
            .Where(x =>
                x.PayoutId == row.PayoutId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new DigitalAssetWithdrawalDto(
            row.Id,
            row.PayoutId,
            row.FinancialAccountId,
            row.DestinationId,
            row.AssetNetworkId,
            row.AssetCode,
            row.AssetNetwork.NetworkCode,
            row.ProviderCode,
            row.Amount,
            row.NetworkFee,
            row.TotalDebitAmount,
            row.Status,
            networkTx?.TransactionHash,
            networkTx?.Confirmations ?? 0,
            networkTx?.RequiredConfirmations
                ?? row.AssetNetwork.RequiredConfirmations,
            row.FailureReason,
            row.CreatedAt,
            row.CompletedAt);
    }

    private static DigitalAssetDepositAddressDto ToDepositAddressDto(
        DigitalAssetDepositAddress x) =>
        new(
            x.Id,
            x.FinancialAccountId,
            x.AssetNetworkId,
            x.AssetNetwork.AssetCode,
            x.AssetNetwork.NetworkCode,
            x.ProviderCode,
            x.Address,
            x.DestinationTag,
            x.Status,
            x.CreatedAt);

    private static DigitalAssetDepositIntentDto ToDepositIntentDto(
        DigitalAssetDepositIntent x) =>
        new(
            x.Id,
            x.FinancialAccountId,
            x.AssetNetworkId,
            x.AssetCode,
            x.NetworkCode,
            x.ProviderCode,
            x.Amount,
            x.Address,
            x.ProviderCollectionId,
            x.ProviderReference,
            x.Status,
            x.ProviderExpiresAt,
            x.CreatedAt,
            x.CompletedAt,
            x.FailureReason);

    private static DigitalAssetWithdrawalDestinationDto ToDestinationDto(
        DigitalAssetWithdrawalDestination x) =>
        new(
            x.Id,
            x.AssetNetworkId,
            x.AssetCode,
            x.AssetNetwork.NetworkCode,
            x.Address,
            x.DestinationTag,
            x.Label,
            x.Status,
            x.CreatedAt);

    private static string TradingCountry(BusinessProfile profile) =>
        (profile.OperatingCountryCode ?? profile.CountryCode)
            .Trim()
            .ToUpperInvariant();

    private static string Required(
        string? value,
        int max,
        string label)
    {
        var clean = Clean(value, max);

        return string.IsNullOrWhiteSpace(clean)
            ? throw new InvalidOperationException(
                $"{label} is required.")
            : clean;
    }

    private static string? Clean(
        string? value,
        int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = value.Trim();
        return clean.Length <= max
            ? clean
            : clean[..max];
    }
}