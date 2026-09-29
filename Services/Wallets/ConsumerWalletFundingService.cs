using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Payments;
using KorridorX.Dtos.Wallets;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Compliance;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Services.References;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerWalletFundingService : IConsumerWalletFundingService
{
    private readonly AppDbContext _db;
    private readonly ICollectionPaymentMethodPolicy _paymentMethodPolicy;
    private readonly ICollectionStatusService _collectionStatusService;
    private readonly IRemittanceProvider _remittanceProvider;
    private readonly IComplianceGateService _complianceGateService;
    private readonly IProviderWalletResolver _providerWallets;
    private readonly IReferenceGenerator _referenceGenerator;

    public ConsumerWalletFundingService(
        AppDbContext db,
        ICollectionPaymentMethodPolicy paymentMethodPolicy,
        ICollectionStatusService collectionStatusService,
        IRemittanceProvider remittanceProvider,
        IComplianceGateService complianceGateService,
        IProviderWalletResolver providerWallets,
        IReferenceGenerator referenceGenerator)
    {
        _db = db;
        _paymentMethodPolicy = paymentMethodPolicy;
        _collectionStatusService = collectionStatusService;
        _remittanceProvider = remittanceProvider;
        _complianceGateService = complianceGateService;
        _providerWallets = providerWallets;
        _referenceGenerator = referenceGenerator;
    }

    public async Task<IReadOnlyList<CollectionPaymentMethodDto>> GetFundingMethodsAsync(
        Guid userId,
        Guid walletId,
        CancellationToken ct = default)
    {
        var context = await LoadContextAsync(userId, walletId, ct);
        return _paymentMethodPolicy
            .GetSupportedMethods(context.Profile.CountryCode, context.Wallet.AssetCode)
            .Where(x => x is PaymentMethod.Card or PaymentMethod.Interac)
            .Select(ToPaymentMethodDto)
            .ToList();
    }

    public async Task<CollectionDetailsDto> CreateCollectionAsync(
        Guid userId,
        Guid walletId,
        CreateConsumerWalletFundingCollectionRequestDto request,
        CancellationToken ct = default)
    {
        var context = await LoadContextAsync(userId, walletId, ct);

        if (request.Amount <= 0m)
        {
            throw new InvalidOperationException("Wallet funding amount must be greater than zero.");
        }

        var rounded = decimal.Round(
            request.Amount,
            context.Wallet.Asset.DecimalPlaces,
            MidpointRounding.AwayFromZero);

        if (rounded != request.Amount)
        {
            throw new InvalidOperationException(
                $"{context.Wallet.AssetCode} wallet funding supports up to {context.Wallet.Asset.DecimalPlaces} decimal places.");
        }

        if (!_paymentMethodPolicy.IsSupported(
                context.Profile.CountryCode,
                context.Wallet.AssetCode,
                request.PaymentMethod) ||
            request.PaymentMethod is not PaymentMethod.Card and not PaymentMethod.Interac)
        {
            throw new InvalidOperationException(
                $"Payment method '{DisplayName(request.PaymentMethod)}' is not available for this {context.Wallet.AssetCode} wallet.");
        }

        var collection = new Collection
        {
            Purpose = PaymentOperationPurpose.AccountFunding,
            FinancialAccountId = context.Wallet.Id,
            FinancialAccount = context.Wallet,
            RelatedEntityType = nameof(FinancialAccount),
            RelatedEntityId = context.Wallet.Id,
            ContextEntityType = "ConsumerWallet",
            ContextEntityId = context.Wallet.Id,
            Reference = await GenerateUniqueCollectionReferenceAsync(ct),
            CurrencyCode = context.Wallet.AssetCode,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = CollectionStatus.Pending,
            ProviderCode = _remittanceProvider.ProviderCode.ToString().ToUpperInvariant(),
            CreatedByUserId = userId
        };

        collection.Attempts.Add(new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = JsonSerializer.Serialize(new
            {
                walletId = context.Wallet.Id,
                collectionReference = collection.Reference,
                amount = request.Amount,
                currencyCode = context.Wallet.AssetCode,
                paymentMethod = request.PaymentMethod.ToString()
            }),
            AttemptedAt = DateTime.UtcNow
        });

        _db.Collections.Add(collection);
        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(collection);
    }

    public async Task<CollectionDetailsDto> InitiateCollectionAsync(
        Guid userId,
        Guid walletId,
        Guid collectionId,
        InitiateCollectionRequestDto request,
        CancellationToken ct = default)
    {
        var context = await LoadContextAsync(userId, walletId, ct);

        var collection = await _db.Collections
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionId &&
                x.Purpose == PaymentOperationPurpose.AccountFunding &&
                x.FinancialAccountId == context.Wallet.Id &&
                !x.TransferId.HasValue &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Wallet funding collection not found.");

        if (collection.Status is CollectionStatus.Initiated or
            CollectionStatus.Processing or
            CollectionStatus.Successful)
        {
            return ToDetailsDto(collection);
        }

        if (collection.Status is not CollectionStatus.Pending and
            not CollectionStatus.Failed and
            not CollectionStatus.Expired)
        {
            throw new InvalidOperationException(
                $"Wallet funding cannot be initiated while its status is '{collection.Status}'.");
        }

        if (!_paymentMethodPolicy.IsSupported(
                context.Profile.CountryCode,
                collection.CurrencyCode,
                collection.PaymentMethod) ||
            collection.PaymentMethod is not PaymentMethod.Card and not PaymentMethod.Interac)
        {
            throw new InvalidOperationException(
                $"Payment method '{DisplayName(collection.PaymentMethod)}' is not available for this wallet.");
        }

        var compliance = await _complianceGateService.EnsureCanInitiateMoneyMovementAsync(
            context.Profile.Id,
            _remittanceProvider.ProviderCode,
            ct);

        var email = request.PayerEmail ?? context.Profile.Email ?? context.Profile.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Payer email is required to fund this wallet.");
        }

        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? $"{context.Profile.FirstName} {context.Profile.LastName}".Trim()
            : request.CustomerName.Trim();

        var providerWalletId = await _providerWallets.SelectAsync(
            "Collection",
            collection.Id,
            _remittanceProvider.ProviderName,
            collection.CurrencyCode,
            null,
            ProviderWalletResolver.Collection,
            collection.Attempts.Count > 1 || collection.ProviderCollectionId != null,
            ct);

        var cardRequest = request.Card;
        var card = cardRequest is null
            ? null
            : new RemittanceCardDetails(
                cardRequest.CardHolderName,
                cardRequest.CardNumber,
                cardRequest.Expiry,
                cardRequest.Cvc);

        var sanitizedRequestPayload = JsonSerializer.Serialize(new
        {
            providerWalletId,
            walletId = context.Wallet.Id,
            collectionId = collection.Id,
            collectionReference = collection.Reference,
            amount = collection.Amount,
            currencyCode = collection.CurrencyCode,
            paymentMethod = collection.PaymentMethod.ToString(),
            payerEmail = email,
            customerName,
            interacExpiryHours = request.InteracExpiryHours,
            redirectUrl = request.RedirectUrl,
            card = card is null ? null : new
            {
                cardHolderName = card.CardHolderName,
                cardNumber = MaskCardNumber(card.CardNumber),
                expiry = card.Expiry,
                cvc = "***REDACTED***"
            }
        });

        var attempt = new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = sanitizedRequestPayload,
            AttemptedAt = DateTime.UtcNow
        };

        collection.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        try
        {
            var providerResult = await _remittanceProvider.InitiateCollectionAsync(
                new RemittanceCollectionRequest(
                    collection.Id,
                    collection.Id,
                    collection.PaymentMethod,
                    collection.Amount,
                    collection.CurrencyCode,
                    compliance.ProviderCustomerId,
                    email,
                    customerName,
                    context.Profile.PhoneNumber,
                    providerWalletId,
                    request.RedirectUrl,
                    card,
                    request.InteracExpiryHours),
                ct);

            await _collectionStatusService.ApplyTransitionAsync(
                collection,
                CollectionStatus.Initiated,
                new CollectionStatusTransitionContext(
                    Source: "WalletFundingProvider",
                    Reason: $"Wallet funding was initiated with {_remittanceProvider.ProviderName}.",
                    ChangedByUserId: userId,
                    ProviderCollectionId: providerResult.ProviderTransactionId,
                    ProviderReference: providerResult.ProviderReference,
                    CheckoutUrl: providerResult.CheckoutUrl,
                    ProviderExpiresAt: providerResult.ExpiresAt,
                    ProviderRequestId: providerResult.ProviderRequestLogId.ToString(),
                    ProviderResponseId: providerResult.ProviderTransactionId,
                    RequestPayloadJson: sanitizedRequestPayload,
                    ResponsePayloadJson: providerResult.RawResponseJson,
                    MetadataJson: JsonSerializer.Serialize(new
                    {
                        walletId = context.Wallet.Id,
                        provider = _remittanceProvider.ProviderName,
                        providerStatus = providerResult.ProviderStatus
                    })),
                attempt,
                ct);

            await UpsertProviderTransactionAsync(collection, providerResult, ct);
            await _db.SaveChangesAsync(ct);
            return ToDetailsDto(collection);
        }
        catch (InvalidOperationException ex)
        {
            attempt.Status = ProviderRequestStatus.Failed;
            attempt.ErrorMessage = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
            attempt.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
        catch (ProviderIntegrationException ex)
        {
            await _collectionStatusService.ApplyTransitionAsync(
                collection,
                CollectionStatus.Failed,
                new CollectionStatusTransitionContext(
                    Source: "WalletFundingProvider",
                    Reason: ex.Message,
                    ChangedByUserId: userId,
                    ProviderRequestId: ex.RequestLogId?.ToString(),
                    RequestPayloadJson: sanitizedRequestPayload,
                    ResponsePayloadJson: ex.ProviderResponse),
                attempt,
                ct);

            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<CollectionDetailsDto> GetCollectionAsync(
        Guid userId,
        Guid walletId,
        Guid collectionId,
        CancellationToken ct = default)
    {
        await LoadContextAsync(userId, walletId, ct);

        var collection = await _db.Collections
            .AsNoTracking()
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionId &&
                x.Purpose == PaymentOperationPurpose.AccountFunding &&
                x.FinancialAccountId == walletId &&
                !x.TransferId.HasValue &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Wallet funding collection not found.");

        return ToDetailsDto(collection);
    }

    private async Task<FundingContext> LoadContextAsync(
        Guid userId,
        Guid walletId,
        CancellationToken ct)
    {
        var profile = await _db.CustomerProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Customer profile not found.");

        var wallet = await _db.FinancialAccounts
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x =>
                x.Id == walletId &&
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                x.AccountType == FinancialAccountType.Customer &&
                x.Status == FinancialAccountStatus.Active &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Consumer wallet not found or inactive.");

        var depositEnabled = await _db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Country)
            .Include(x => x.Asset)
            .AnyAsync(x =>
                x.CountryCode == profile.CountryCode &&
                x.AssetCode == wallet.AssetCode &&
                x.Country.IsSupported &&
                x.Asset.IsSupported &&
                x.Asset.Type == AssetType.Fiat &&
                x.CanDeposit,
                ct);

        if (!depositEnabled)
        {
            throw new InvalidOperationException(
                $"Deposits are not enabled for the {wallet.AssetCode} wallet in {profile.CountryCode}.");
        }

        return new FundingContext(profile, wallet);
    }

    private async Task UpsertProviderTransactionAsync(
        Collection collection,
        RemittanceCollectionResult providerResult,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == _remittanceProvider.ProviderCode &&
                x.ProviderTransactionId == providerResult.ProviderTransactionId,
                ct);

        if (transaction is null)
        {
            _db.ProviderTransactions.Add(new ProviderTransaction
            {
                ProviderCode = _remittanceProvider.ProviderCode,
                TransferId = null,
                CollectionId = collection.Id,
                ProviderTransactionId = providerResult.ProviderTransactionId,
                ProviderReference = providerResult.ProviderReference,
                TransactionType = "WalletFundingCollection",
                ProviderStatus = providerResult.ProviderStatus,
                CurrencyCode = collection.CurrencyCode,
                Amount = collection.Amount,
                RawPayloadJson = providerResult.RawResponseJson,
                LastSyncedAt = DateTime.UtcNow
            });
            return;
        }

        transaction.CollectionId = collection.Id;
        transaction.ProviderReference = providerResult.ProviderReference;
        transaction.ProviderStatus = providerResult.ProviderStatus;
        transaction.RawPayloadJson = providerResult.RawResponseJson;
        transaction.LastSyncedAt = DateTime.UtcNow;
        transaction.LastUpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> GenerateUniqueCollectionReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateCollectionReference();
            if (!await _db.Collections.AsNoTracking().AnyAsync(x => x.Reference == reference, ct))
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique collection reference.");
    }

    private static CollectionPaymentMethodDto ToPaymentMethodDto(PaymentMethod method) =>
        new(method, method.ToString(), DisplayName(method));

    private static string DisplayName(PaymentMethod method) =>
        method switch
        {
            PaymentMethod.Interac => "Interac",
            PaymentMethod.Card => "Card",
            _ => method.ToString()
        };

    private static string MaskCardNumber(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"************{digits[^4..]}" : "****";
    }

    private static CollectionDetailsDto ToDetailsDto(Collection collection) =>
        new(
            new CollectionDto
            {
                Id = collection.Id,
                TransferId = collection.TransferId,
                Purpose = collection.Purpose,
                FinancialAccountId = collection.FinancialAccountId,
                RelatedEntityType = collection.RelatedEntityType,
                RelatedEntityId = collection.RelatedEntityId,
                ContextEntityType = collection.ContextEntityType,
                ContextEntityId = collection.ContextEntityId,
                Reference = collection.Reference,
                CurrencyCode = collection.CurrencyCode,
                Amount = collection.Amount,
                PaymentMethod = collection.PaymentMethod,
                Status = collection.Status,
                ProviderCode = collection.ProviderCode,
                ProviderCollectionId = collection.ProviderCollectionId,
                ProviderReference = collection.ProviderReference,
                CheckoutUrl = collection.CheckoutUrl,
                ProviderExpiresAt = collection.ProviderExpiresAt,
                InitiatedAt = collection.InitiatedAt,
                ConfirmedAt = collection.ConfirmedAt,
                FailedAt = collection.FailedAt,
                ExpiredAt = collection.ExpiredAt,
                FailureReason = collection.FailureReason,
                CreatedAt = collection.CreatedAt,
                LastUpdatedAt = collection.LastUpdatedAt
            },
            collection.Attempts
                .OrderByDescending(x => x.AttemptedAt)
                .Select(x => new CollectionAttemptDto(
                    x.Id,
                    x.CollectionId,
                    x.Status,
                    x.ProviderRequestId,
                    x.ProviderResponseId,
                    x.RequestPayloadJson,
                    x.ResponsePayloadJson,
                    x.AttemptedAt,
                    x.ErrorMessage))
                .ToList());

    private sealed record FundingContext(
        KorridorX.Models.Customers.CustomerProfile Profile,
        FinancialAccount Wallet);
}
