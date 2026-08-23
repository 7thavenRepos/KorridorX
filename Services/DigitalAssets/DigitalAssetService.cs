using System.Data;
using KorridorX.Data;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.FinancialCore;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetService : IEmbeddedDigitalAssetService, IDigitalAssetSettlementService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedFinanceContextAccessor _context;
    private readonly IDigitalAssetProviderRegistry _providers;
    private readonly IFinancialReservationService _reservations;
    private readonly IDigitalAssetComplianceGate _compliance;
    private readonly IEmbeddedWebhookPublisher _webhooks;

    public DigitalAssetService(
        AppDbContext db,
        IEmbeddedFinanceContextAccessor context,
        IDigitalAssetProviderRegistry providers,
        IFinancialReservationService reservations,
        IDigitalAssetComplianceGate compliance,
        IEmbeddedWebhookPublisher webhooks)
    {
        _db = db;
        _context = context;
        _providers = providers;
        _reservations = reservations;
        _compliance = compliance;
        _webhooks = webhooks;
    }

    public async Task<IReadOnlyList<DigitalAssetDepositAddressDto>> GetDepositAddressesAsync(Guid businessCustomerId, CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.DigitalAssetsRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        var rows = await _db.DigitalAssetDepositAddresses.AsNoTracking().Include(x => x.AssetNetwork)
            .Where(x => x.BusinessProfileId == principal.BusinessProfileId && x.BusinessCustomerId == businessCustomerId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDepositAddressDto).ToList();
    }

    public async Task<DigitalAssetDepositAddressDto> CreateDepositAddressAsync(Guid businessCustomerId, CreateDigitalAssetDepositAddressRequestDto request, CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.DigitalAssetsWrite);
        var customer = await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        EnsureActiveCustomer(customer);

        var account = await GetCustomerAccountAsync(businessCustomerId, request.FinancialAccountId, ct);
        var network = await GetNetworkAsync(request.AssetNetworkId, account.AssetCode, true, ct);
        var provider = _providers.GetRequired(request.ProviderCode, account.AssetCode, network.NetworkCode);

        var existing = await _db.DigitalAssetDepositAddresses.AsNoTracking().Include(x => x.AssetNetwork)
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.FinancialAccountId == account.Id &&
                x.AssetNetworkId == network.Id &&
                x.ProviderCode == provider.ProviderCode &&
                x.Status == DigitalAssetAddressStatus.Active &&
                !x.IsDeleted, ct);

        if (existing is not null) return ToDepositAddressDto(existing);

        var result = await provider.CreateDepositAddressAsync(
            new DigitalAssetDepositAddressRequest(principal.BusinessProfileId, businessCustomerId, account.Id, account.AssetCode, network.NetworkCode), ct);

        var address = new DigitalAssetDepositAddress
        {
            BusinessProfileId = principal.BusinessProfileId,
            BusinessCustomerId = businessCustomerId,
            FinancialAccountId = account.Id,
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

    public async Task<IReadOnlyList<DigitalAssetWithdrawalDestinationDto>> GetWithdrawalDestinationsAsync(Guid businessCustomerId, CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.DigitalAssetsRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        var rows = await _db.DigitalAssetWithdrawalDestinations.AsNoTracking().Include(x => x.AssetNetwork)
            .Where(x => x.BusinessProfileId == principal.BusinessProfileId && x.BusinessCustomerId == businessCustomerId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDestinationDto).ToList();
    }

    public async Task<DigitalAssetWithdrawalDestinationDto> CreateWithdrawalDestinationAsync(Guid businessCustomerId, CreateDigitalAssetWithdrawalDestinationRequestDto request, CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.DigitalAssetsWrite);
        var customer = await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        EnsureActiveCustomer(customer);

        var network = await _db.AssetNetworks.Include(x => x.Asset).FirstOrDefaultAsync(x => x.Id == request.AssetNetworkId, ct)
            ?? throw new InvalidOperationException("Asset network not found.");
        ValidateNetwork(network, network.AssetCode, false);

        var destination = new DigitalAssetWithdrawalDestination
        {
            BusinessProfileId = principal.BusinessProfileId,
            BusinessCustomerId = businessCustomerId,
            AssetNetworkId = network.Id,
            AssetNetwork = network,
            AssetCode = network.AssetCode,
            Address = Required(request.Address, 300, "Withdrawal address"),
            DestinationTag = Clean(request.DestinationTag, 150),
            Label = Clean(request.Label, 150),
            Status = DigitalAssetDestinationStatus.Active
        };

        var existing = await _db.DigitalAssetWithdrawalDestinations.AsNoTracking().Include(x => x.AssetNetwork)
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.AssetNetworkId == destination.AssetNetworkId &&
                x.Address == destination.Address &&
                x.DestinationTag == destination.DestinationTag &&
                !x.IsDeleted, ct);

        if (existing is not null) return ToDestinationDto(existing);

        _db.DigitalAssetWithdrawalDestinations.Add(destination);
        await _db.SaveChangesAsync(ct);
        return ToDestinationDto(destination);
    }

    public async Task<DigitalAssetWithdrawalDto> CreateWithdrawalAsync(Guid businessCustomerId, CreateDigitalAssetWithdrawalRequestDto request, CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.DigitalAssetsWrite);
        var customer = await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        EnsureActiveCustomer(customer);
        if (request.Amount <= 0m) throw new InvalidOperationException("Withdrawal amount must be greater than zero.");

        var account = await GetCustomerAccountAsync(businessCustomerId, request.FinancialAccountId, ct);
        var destination = await _db.DigitalAssetWithdrawalDestinations.Include(x => x.AssetNetwork).ThenInclude(x => x.Asset)
            .FirstOrDefaultAsync(x =>
                x.Id == request.DestinationId &&
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Digital-asset withdrawal destination not found.");

        if (destination.Status != DigitalAssetDestinationStatus.Active)
            throw new InvalidOperationException("Digital-asset withdrawal destination is not active.");

        ValidateNetwork(destination.AssetNetwork, account.AssetCode, false);
        if (request.Amount < destination.AssetNetwork.MinimumWithdrawal)
            throw new InvalidOperationException($"Minimum withdrawal is {destination.AssetNetwork.MinimumWithdrawal} {account.AssetCode}.");

        var fee = destination.AssetNetwork.WithdrawalFee;
        var totalDebit = request.Amount + fee;

        await _compliance.EnsureWithdrawalAllowedAsync(
            principal.BusinessProfileId, businessCustomerId, account.AssetCode,
            destination.AssetNetwork.NetworkCode, request.Amount, destination.Address, ct);

        var provider = _providers.GetRequired(request.ProviderCode, account.AssetCode, destination.AssetNetwork.NetworkCode);

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var payout = new Payout
        {
            Purpose = PaymentOperationPurpose.Withdrawal,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            RelatedEntityType = nameof(DigitalAssetWithdrawal),
            ContextEntityType = nameof(BusinessCustomer),
            ContextEntityId = businessCustomerId,
            Reference = Clean(request.ExternalReference, 80) ?? $"DAX-WD-{Guid.NewGuid():N}",
            CurrencyCode = account.AssetCode,
            Amount = request.Amount,
            PaymentMethod = PaymentMethod.DigitalAsset,
            Status = PayoutStatus.Pending,
            ProviderCode = provider.ProviderCode
        };

        var withdrawal = new DigitalAssetWithdrawal
        {
            BusinessProfileId = principal.BusinessProfileId,
            BusinessCustomerId = businessCustomerId,
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

        var reservation = await _reservations.ReserveAsync(
            account.Id, FinancialReservationType.Withdrawal, nameof(DigitalAssetWithdrawal),
            withdrawal.Id, totalDebit, null, nameof(Payout), payout.Id, ct);

        withdrawal.ReservationId = reservation.Id;
        withdrawal.Reservation = reservation;
        await _db.SaveChangesAsync(ct);

        try
        {
            var submission = await provider.SubmitWithdrawalAsync(
                new DigitalAssetWithdrawalSubmissionRequest(
                    withdrawal.Id, account.AssetCode, destination.AssetNetwork.NetworkCode,
                    destination.Address, destination.DestinationTag, request.Amount, payout.Reference), ct);

            var providerTransactionId = Required(submission.ProviderTransactionId, 200, "Provider transaction ID");

            var networkTx = new DigitalAssetNetworkTransaction
            {
                ProviderCode = provider.ProviderCode,
                ProviderTransactionId = providerTransactionId,
                ProviderReference = Clean(submission.ProviderReference, 200),
                AssetNetworkId = destination.AssetNetworkId,
                AssetNetwork = destination.AssetNetwork,
                AssetCode = account.AssetCode,
                Direction = DigitalAssetTransactionDirection.Outbound,
                Status = DigitalAssetTransactionStatus.Confirming,
                TransactionHash = Clean(submission.TransactionHash, 300),
                ToAddress = destination.Address,
                DestinationTag = destination.DestinationTag,
                Amount = request.Amount,
                NetworkFee = fee,
                RequiredConfirmations = destination.AssetNetwork.RequiredConfirmations,
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

            await _webhooks.PublishAsync(principal.BusinessProfileId, "digital_asset.withdrawal.submitted", new
            {
                id = withdrawal.Id, payoutId = payout.Id, businessCustomerId,
                assetCode = account.AssetCode, networkCode = destination.AssetNetwork.NetworkCode,
                amount = request.Amount, fee, transactionHash = networkTx.TransactionHash,
                providerTransactionId
            }, ct);

            return await ToWithdrawalDtoAsync(withdrawal.Id, ct);
        }
        catch (Exception ex)
        {
            if (reservation.Status == FinancialReservationStatus.Active && reservation.RemainingAmount > 0m)
                await _reservations.ReleaseAsync(reservation.Id, reservation.RemainingAmount, "Digital-asset provider submission failed.", null, ct);

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

    public async Task<IReadOnlyList<DigitalAssetWithdrawalDto>> GetWithdrawalsAsync(Guid businessCustomerId, CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.DigitalAssetsRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        var ids = await _db.DigitalAssetWithdrawals.AsNoTracking()
            .Where(x => x.BusinessProfileId == principal.BusinessProfileId && x.BusinessCustomerId == businessCustomerId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(ct);
        var result = new List<DigitalAssetWithdrawalDto>();
        foreach (var id in ids) result.Add(await ToWithdrawalDtoAsync(id, ct));
        return result;
    }

    public async Task ProcessInboundAsync(DigitalAssetInboundNotification notification, CancellationToken ct = default)
    {
        if (notification.Amount <= 0m) throw new InvalidOperationException("Inbound digital-asset amount must be greater than zero.");
        var providerCode = Required(notification.ProviderCode, 50, "Provider code");
        var providerTransactionId = Required(notification.ProviderTransactionId, 200, "Provider transaction ID");
        var assetCode = Required(notification.AssetCode, 20, "Asset code").ToUpperInvariant();

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var existing = await _db.DigitalAssetNetworkTransactions.Include(x => x.AssetNetwork)
            .FirstOrDefaultAsync(x => x.ProviderCode == providerCode && x.ProviderTransactionId == providerTransactionId && !x.IsDeleted, ct);

        if (existing is not null)
        {
            await UpdateInboundTransactionAsync(existing, notification, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return;
        }

        var tag = Clean(notification.DestinationTag, 150);
        var address = await _db.DigitalAssetDepositAddresses.Include(x => x.AssetNetwork).ThenInclude(x => x.Asset)
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == providerCode &&
                x.AssetNetworkId == notification.AssetNetworkId &&
                x.Address == notification.ToAddress &&
                x.DestinationTag == tag &&
                x.Status == DigitalAssetAddressStatus.Active &&
                !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Inbound digital-asset transaction does not match an active deposit address.");

        if (!string.Equals(address.FinancialAccount.AssetCode, assetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Inbound digital-asset transaction asset does not match the destination account.");

        ValidateNetwork(address.AssetNetwork, assetCode, true);
        if (notification.Amount < address.AssetNetwork.MinimumDeposit)
            throw new InvalidOperationException($"Minimum deposit is {address.AssetNetwork.MinimumDeposit} {assetCode}.");

        await _compliance.EnsureDepositAllowedAsync(
            address.BusinessProfileId, address.BusinessCustomerId, assetCode,
            address.AssetNetwork.NetworkCode, notification.Amount, notification.FromAddress, ct);

        var networkTx = new DigitalAssetNetworkTransaction
        {
            ProviderCode = providerCode,
            ProviderTransactionId = providerTransactionId,
            ProviderReference = Clean(notification.ProviderReference, 200),
            AssetNetworkId = address.AssetNetworkId,
            AssetNetwork = address.AssetNetwork,
            AssetCode = assetCode,
            Direction = DigitalAssetTransactionDirection.Inbound,
            Status = DigitalAssetTransactionStatus.Observed,
            TransactionHash = Clean(notification.TransactionHash, 300),
            FromAddress = Clean(notification.FromAddress, 300),
            ToAddress = address.Address,
            DestinationTag = address.DestinationTag,
            Amount = notification.Amount,
            Confirmations = Math.Max(0, notification.Confirmations),
            RequiredConfirmations = address.AssetNetwork.RequiredConfirmations,
            BlockNumber = notification.BlockNumber,
            RawPayloadJson = notification.RawPayloadJson,
            ObservedAt = notification.ObservedAt == default ? DateTime.UtcNow : notification.ObservedAt.ToUniversalTime()
        };

        _db.DigitalAssetNetworkTransactions.Add(networkTx);
        address.LastUsedAt = DateTime.UtcNow;
        address.LastUpdatedAt = DateTime.UtcNow;

        await ApplyInboundConfirmationAsync(networkTx, address, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ProcessOutboundAsync(DigitalAssetOutboundNotification notification, CancellationToken ct = default)
    {
        var providerCode = Required(notification.ProviderCode, 50, "Provider code");
        var providerTransactionId = Required(notification.ProviderTransactionId, 200, "Provider transaction ID");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var networkTx = await _db.DigitalAssetNetworkTransactions.Include(x => x.AssetNetwork)
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == providerCode &&
                x.ProviderTransactionId == providerTransactionId &&
                x.Direction == DigitalAssetTransactionDirection.Outbound &&
                !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Outbound digital-asset transaction not found.");

        var withdrawal = await _db.DigitalAssetWithdrawals.Include(x => x.Payout).Include(x => x.Reservation)
            .FirstAsync(x => x.PayoutId == networkTx.PayoutId, ct);

        networkTx.Confirmations = Math.Max(networkTx.Confirmations, notification.Confirmations);
        networkTx.TransactionHash = Clean(notification.TransactionHash, 300) ?? networkTx.TransactionHash;
        networkTx.BlockNumber = notification.BlockNumber ?? networkTx.BlockNumber;
        networkTx.ProviderReference = Clean(notification.ProviderReference, 200) ?? networkTx.ProviderReference;
        networkTx.RawPayloadJson = notification.RawPayloadJson ?? networkTx.RawPayloadJson;
        if (notification.NetworkFee.HasValue && notification.NetworkFee.Value >= 0m) networkTx.NetworkFee = notification.NetworkFee.Value;
        networkTx.LastUpdatedAt = DateTime.UtcNow;

        var status = (notification.Status ?? "").Trim().ToUpperInvariant();
        var confirmed = status is "SUCCESSFUL" or "COMPLETED" or "CONFIRMED";
        var failed = status is "FAILED" or "REJECTED" or "CANCELLED";

        if (failed)
        {
            if (withdrawal.Reservation.Status == FinancialReservationStatus.Active && withdrawal.Reservation.RemainingAmount > 0m)
                await _reservations.ReleaseAsync(withdrawal.ReservationId, withdrawal.Reservation.RemainingAmount, "Digital-asset withdrawal failed on provider/network.", null, ct);

            networkTx.Status = DigitalAssetTransactionStatus.Failed;
            networkTx.FailedAt = DateTime.UtcNow;
            withdrawal.Status = DigitalAssetWithdrawalStatus.Failed;
            withdrawal.FailedAt = DateTime.UtcNow;
            withdrawal.FailureReason = $"Provider/network status: {status}";
            withdrawal.Payout.Status = PayoutStatus.Failed;
            withdrawal.Payout.FailedAt = DateTime.UtcNow;
            withdrawal.Payout.FailureReason = withdrawal.FailureReason;
        }
        else if (confirmed && networkTx.Confirmations >= networkTx.RequiredConfirmations)
        {
            if (withdrawal.Reservation.Status == FinancialReservationStatus.Active && withdrawal.Reservation.RemainingAmount > 0m)
                await _reservations.CaptureAsync(withdrawal.ReservationId, withdrawal.Reservation.RemainingAmount, null, ct);

            networkTx.Status = DigitalAssetTransactionStatus.Confirmed;
            networkTx.ConfirmedAt = DateTime.UtcNow;
            withdrawal.Status = DigitalAssetWithdrawalStatus.Completed;
            withdrawal.CompletedAt = DateTime.UtcNow;
            withdrawal.Payout.Status = PayoutStatus.Successful;
            withdrawal.Payout.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            networkTx.Status = DigitalAssetTransactionStatus.Confirming;
            withdrawal.Status = DigitalAssetWithdrawalStatus.Confirming;
            withdrawal.Payout.Status = PayoutStatus.Processing;
        }

        withdrawal.LastUpdatedAt = DateTime.UtcNow;
        withdrawal.Payout.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _webhooks.PublishAsync(withdrawal.BusinessProfileId,
            networkTx.Status == DigitalAssetTransactionStatus.Confirmed
                ? "digital_asset.withdrawal.completed"
                : networkTx.Status == DigitalAssetTransactionStatus.Failed
                    ? "digital_asset.withdrawal.failed"
                    : "digital_asset.withdrawal.confirming",
            new
            {
                id = withdrawal.Id, payoutId = withdrawal.PayoutId,
                businessCustomerId = withdrawal.BusinessCustomerId,
                assetCode = withdrawal.AssetCode, amount = withdrawal.Amount,
                networkFee = networkTx.NetworkFee, confirmations = networkTx.Confirmations,
                requiredConfirmations = networkTx.RequiredConfirmations,
                transactionHash = networkTx.TransactionHash, status = withdrawal.Status.ToString()
            }, ct);
    }

    private async Task UpdateInboundTransactionAsync(DigitalAssetNetworkTransaction existing, DigitalAssetInboundNotification notification, CancellationToken ct)
    {
        if (existing.Status == DigitalAssetTransactionStatus.Confirmed) return;
        if (existing.Amount != notification.Amount || !string.Equals(existing.AssetCode, notification.AssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider transaction replay contains different digital-asset details.");

        var address = await _db.DigitalAssetDepositAddresses.Include(x => x.FinancialAccount).Include(x => x.AssetNetwork).ThenInclude(x => x.Asset)
            .FirstAsync(x =>
                x.ProviderCode == existing.ProviderCode &&
                x.AssetNetworkId == existing.AssetNetworkId &&
                x.Address == existing.ToAddress &&
                x.DestinationTag == existing.DestinationTag, ct);

        existing.Confirmations = Math.Max(existing.Confirmations, notification.Confirmations);
        existing.TransactionHash = Clean(notification.TransactionHash, 300) ?? existing.TransactionHash;
        existing.BlockNumber = notification.BlockNumber ?? existing.BlockNumber;
        existing.RawPayloadJson = notification.RawPayloadJson ?? existing.RawPayloadJson;
        existing.LastUpdatedAt = DateTime.UtcNow;
        await ApplyInboundConfirmationAsync(existing, address, ct);
    }

    private async Task ApplyInboundConfirmationAsync(DigitalAssetNetworkTransaction networkTx, DigitalAssetDepositAddress address, CancellationToken ct)
    {
        if (networkTx.Confirmations < networkTx.RequiredConfirmations)
        {
            networkTx.Status = DigitalAssetTransactionStatus.Confirming;
            return;
        }
        if (networkTx.CollectionId.HasValue)
        {
            networkTx.Status = DigitalAssetTransactionStatus.Confirmed;
            return;
        }

        var account = address.FinancialAccount;
        if (account.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Destination Financial Account is not active.");

        var now = DateTime.UtcNow;
        var collection = new Collection
        {
            Purpose = PaymentOperationPurpose.AccountFunding,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            RelatedEntityType = nameof(DigitalAssetDepositAddress),
            RelatedEntityId = address.Id,
            ContextEntityType = nameof(BusinessCustomer),
            ContextEntityId = address.BusinessCustomerId,
            Reference = $"DAX-DEP-{Guid.NewGuid():N}"[..40],
            CurrencyCode = account.AssetCode,
            Amount = networkTx.Amount,
            PaymentMethod = PaymentMethod.DigitalAsset,
            Status = CollectionStatus.Successful,
            ProviderCode = networkTx.ProviderCode,
            ProviderCollectionId = networkTx.ProviderTransactionId,
            ProviderReference = networkTx.ProviderReference ?? networkTx.TransactionHash,
            InitiatedAt = networkTx.ObservedAt,
            ConfirmedAt = now
        };
        _db.Collections.Add(collection);

        account.SettledBalance += networkTx.Amount;
        account.AvailableBalance += networkTx.Amount;
        account.LastUpdatedAt = now;

        var ledger = new LedgerTransaction
        {
            Reference = $"LED-DAX-{Guid.NewGuid():N}"[..40],
            AssetCode = account.AssetCode,
            Type = LedgerTransactionType.Deposit,
            Status = LedgerTransactionStatus.Posted,
            Amount = networkTx.Amount,
            Description = $"Confirmed {account.AssetCode} deposit on {address.AssetNetwork.NetworkCode}.",
            IdempotencyScope = $"DigitalAssetDeposit:{networkTx.ProviderCode}",
            IdempotencyKey = networkTx.ProviderTransactionId,
            RelatedEntityType = nameof(Collection),
            RelatedEntityId = collection.Id,
            ContextEntityType = nameof(DigitalAssetNetworkTransaction),
            ContextEntityId = networkTx.Id,
            PostedAt = now
        };

        ledger.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = account.Id, FinancialAccount = account,
            BalanceBucket = LedgerBalanceBucket.External, Side = LedgerPostingSide.Debit,
            Amount = networkTx.Amount
        });
        ledger.Postings.Add(new LedgerPosting
        {
            FinancialAccountId = account.Id, FinancialAccount = account,
            BalanceBucket = LedgerBalanceBucket.Available, Side = LedgerPostingSide.Credit,
            Amount = networkTx.Amount, AccountBalanceAfter = account.AvailableBalance
        });
        _db.LedgerTransactions.Add(ledger);

        networkTx.CollectionId = collection.Id;
        networkTx.Collection = collection;
        networkTx.LedgerTransactionId = ledger.Id;
        networkTx.LedgerTransaction = ledger;
        networkTx.Status = DigitalAssetTransactionStatus.Confirmed;
        networkTx.ConfirmedAt = now;

        await _webhooks.PublishAsync(address.BusinessProfileId, "digital_asset.deposit.completed", new
        {
            id = networkTx.Id, collectionId = collection.Id,
            businessCustomerId = address.BusinessCustomerId, financialAccountId = account.Id,
            assetCode = account.AssetCode, networkCode = address.AssetNetwork.NetworkCode,
            amount = networkTx.Amount, confirmations = networkTx.Confirmations,
            requiredConfirmations = networkTx.RequiredConfirmations, transactionHash = networkTx.TransactionHash
        }, ct);
    }

    private async Task<BusinessCustomer> EnsureCustomerAsync(Guid businessProfileId, Guid businessCustomerId, CancellationToken ct) =>
        await _db.BusinessCustomers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == businessCustomerId && x.BusinessProfileId == businessProfileId && !x.IsDeleted, ct)
        ?? throw new InvalidOperationException("Business customer not found.");

    private static void EnsureActiveCustomer(BusinessCustomer customer)
    {
        if (customer.Status != BusinessCustomerStatus.Active)
            throw new InvalidOperationException("Business customer must be active.");
    }

    private async Task<FinancialAccount> GetCustomerAccountAsync(Guid businessCustomerId, Guid financialAccountId, CancellationToken ct)
    {
        var account = await _db.FinancialAccounts.FirstOrDefaultAsync(x =>
            x.Id == financialAccountId &&
            x.OwnerType == FinancialAccountOwnerType.BusinessCustomer &&
            x.OwnerId == businessCustomerId &&
            x.AccountType == FinancialAccountType.Customer &&
            !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business customer Financial Account not found.");

        if (account.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Financial Account must be active.");
        return account;
    }

    private async Task<KorridorX.Models.Lookups.AssetNetwork> GetNetworkAsync(Guid assetNetworkId, string assetCode, bool deposit, CancellationToken ct)
    {
        var network = await _db.AssetNetworks.Include(x => x.Asset).FirstOrDefaultAsync(x => x.Id == assetNetworkId, ct)
            ?? throw new InvalidOperationException("Asset network not found.");
        ValidateNetwork(network, assetCode, deposit);
        return network;
    }

    private static void ValidateNetwork(KorridorX.Models.Lookups.AssetNetwork network, string assetCode, bool deposit)
    {
        if (!string.Equals(network.AssetCode, assetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Asset network does not match the Financial Account asset.");
        if (network.Asset.Type != AssetType.Crypto || !network.Asset.IsSupported)
            throw new InvalidOperationException("Asset is not an enabled digital asset.");
        if (network.Status != AssetNetworkStatus.Active)
            throw new InvalidOperationException("Asset network is not active.");
        if (deposit && (!network.Asset.DepositEnabled || !network.DepositEnabled))
            throw new InvalidOperationException("Digital-asset deposits are disabled for this network.");
        if (!deposit && (!network.Asset.WithdrawalEnabled || !network.WithdrawalEnabled))
            throw new InvalidOperationException("Digital-asset withdrawals are disabled for this network.");
    }

    private EmbeddedFinancePrincipal RequireScope(EmbeddedFinanceScope scope)
    {
        var principal = _context.GetRequiredPrincipal();
        if (!principal.Scopes.HasFlag(scope))
            throw new UnauthorizedAccessException($"API application does not have the {scope} scope.");
        return principal;
    }

    private async Task<DigitalAssetWithdrawalDto> ToWithdrawalDtoAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.DigitalAssetWithdrawals.AsNoTracking().Include(x => x.AssetNetwork).FirstAsync(x => x.Id == id, ct);
        var networkTx = await _db.DigitalAssetNetworkTransactions.AsNoTracking()
            .Where(x => x.PayoutId == row.PayoutId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

        return new DigitalAssetWithdrawalDto(
            row.Id, row.PayoutId, row.FinancialAccountId, row.DestinationId, row.AssetNetworkId,
            row.AssetCode, row.AssetNetwork.NetworkCode, row.ProviderCode, row.Amount, row.NetworkFee,
            row.TotalDebitAmount, row.Status, networkTx?.TransactionHash, networkTx?.Confirmations ?? 0,
            networkTx?.RequiredConfirmations ?? row.AssetNetwork.RequiredConfirmations,
            row.FailureReason, row.CreatedAt, row.CompletedAt);
    }

    private static DigitalAssetDepositAddressDto ToDepositAddressDto(DigitalAssetDepositAddress x) =>
        new(x.Id, x.FinancialAccountId, x.AssetNetworkId, x.AssetNetwork.AssetCode, x.AssetNetwork.NetworkCode,
            x.ProviderCode, x.Address, x.DestinationTag, x.Status, x.CreatedAt);

    private static DigitalAssetWithdrawalDestinationDto ToDestinationDto(DigitalAssetWithdrawalDestination x) =>
        new(x.Id, x.AssetNetworkId, x.AssetCode, x.AssetNetwork.NetworkCode, x.Address, x.DestinationTag, x.Label, x.Status, x.CreatedAt);

    private static string Required(string? value, int max, string label)
    {
        var clean = Clean(value, max);
        return string.IsNullOrWhiteSpace(clean) ? throw new InvalidOperationException($"{label} is required.") : clean;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();
        return clean.Length <= max ? clean : clean[..max];
    }
}
