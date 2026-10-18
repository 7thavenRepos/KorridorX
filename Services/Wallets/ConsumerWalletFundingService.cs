using KorridorX.Services.PaymentChannels;
using System.Data;
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
    private readonly PaymentChannelService _channels;
    private readonly WalletFundingContextService _contexts;

    public ConsumerWalletFundingService(
        AppDbContext db,
        ICollectionPaymentMethodPolicy paymentMethodPolicy,
        ICollectionStatusService collectionStatusService,
        IRemittanceProvider remittanceProvider,
        IComplianceGateService complianceGateService,
        IProviderWalletResolver providerWallets,
        IReferenceGenerator referenceGenerator, PaymentChannelService channels, WalletFundingContextService contexts)
    {
        _db = db;
        _paymentMethodPolicy = paymentMethodPolicy;
        _collectionStatusService = collectionStatusService;
        _remittanceProvider = remittanceProvider;
        _complianceGateService = complianceGateService;
        _providerWallets = providerWallets;
        _referenceGenerator = referenceGenerator;
        _channels = channels; _contexts = contexts;
    }

    public Task<IReadOnlyList<CollectionPaymentMethodDto>> GetFundingMethodsAsync(Guid userId, Guid walletId, CancellationToken ct = default) =>
        GetMethodsCoreAsync(userId, walletId, false, ct);
    public Task<IReadOnlyList<CollectionPaymentMethodDto>> GetBusinessFundingMethodsAsync(Guid userId, Guid walletId, CancellationToken ct = default) =>
        GetMethodsCoreAsync(userId, walletId, true, ct);
    private async Task<IReadOnlyList<CollectionPaymentMethodDto>> GetMethodsCoreAsync(Guid userId, Guid walletId, bool business, CancellationToken ct)
    {
        var context = await _contexts.LoadAsync(userId, walletId, business, false, true, ct);
        return await _channels.DepositMethodsAsync(context.CountryCode, context.Wallet.AssetCode, business, true, null, ct);
    }
    public Task<CollectionDetailsDto> CreateCollectionAsync(Guid userId, Guid walletId, CreateConsumerWalletFundingCollectionRequestDto request, CancellationToken ct = default) =>
        CreateCoreAsync(userId, walletId, request, false, ct);
    public Task<CollectionDetailsDto> CreateBusinessCollectionAsync(Guid userId, Guid walletId, CreateConsumerWalletFundingCollectionRequestDto request, CancellationToken ct = default) =>
        CreateCoreAsync(userId, walletId, request, true, ct);
    private async Task<CollectionDetailsDto> CreateCoreAsync(Guid userId, Guid walletId,
        CreateConsumerWalletFundingCollectionRequestDto request, bool business, CancellationToken ct)
    {
        var context = await _contexts.LoadAsync(userId, walletId, business, true, true, ct);
        var key = request.IdempotencyKey?.Trim();
        if (key is not null && (key.Length < 8 || key.Length > 100 || !System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z0-9_-]+$")))
            throw new InvalidOperationException("Invalid payment request identifier.");
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (key is not null)
        {
            var lockKey = "WalletCollection:" + walletId + ":" + key;
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
            var replay = await _db.Collections.Include(x => x.Attempts).SingleOrDefaultAsync(x => x.FinancialAccountId == walletId && x.ClientRequestKey == key && !x.IsDeleted, ct);
            if (replay is not null)
            {
                if (replay.Amount != request.Amount || replay.PaymentMethod != request.PaymentMethod || (request.ChannelId.HasValue && replay.PaymentChannelId != request.ChannelId))
                    throw new InvalidOperationException("The payment request identifier was reused with different details.");
                await tx.CommitAsync(ct); return ToDetailsDto(replay);
            }
        }

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

        if (request.PaymentMethod is not PaymentMethod.Card and not PaymentMethod.Interac || business && request.PaymentMethod == PaymentMethod.Card)
            throw new InvalidOperationException("Select an executable collection channel. Bank-account deposits use the deposit-account setup action, not a fixed-amount collection.");
        var choice = await _channels.RequireDepositAsync(request.ChannelId, context.CountryCode,
            context.Wallet.AssetCode, business, true, request.PaymentMethod, request.Amount, ct);

        var collection = new Collection
        {
            Purpose = PaymentOperationPurpose.AccountFunding,
            FinancialAccountId = context.Wallet.Id,
            FinancialAccount = context.Wallet,
            RelatedEntityType = nameof(FinancialAccount),
            RelatedEntityId = context.Wallet.Id,
            ContextEntityType = business ? "BusinessWallet" : "ConsumerWallet",
            ContextEntityId = context.Wallet.Id,
            ClientRequestKey = key,
            PaymentChannelId = choice?.Channel.Id, PaymentChannelName = choice?.Channel.DisplayName,
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

        if (choice is not null) _channels.Record(choice, "Collection", collection.Id, context.CountryCode,
            business, context.OwnerId, walletId, collection.Amount);
        _db.Collections.Add(collection);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDetailsDto(collection);
    }

    public Task<CollectionDetailsDto> InitiateCollectionAsync(Guid userId, Guid walletId, Guid collectionId, InitiateCollectionRequestDto request, CancellationToken ct = default) =>
        InitiateCoreAsync(userId, walletId, collectionId, request, false, ct);
    public Task<CollectionDetailsDto> InitiateBusinessCollectionAsync(Guid userId, Guid walletId, Guid collectionId, InitiateCollectionRequestDto request, CancellationToken ct = default) =>
        InitiateCoreAsync(userId, walletId, collectionId, request, true, ct);
    private async Task<CollectionDetailsDto> InitiateCoreAsync(Guid userId, Guid walletId, Guid collectionId,
        InitiateCollectionRequestDto request, bool business, CancellationToken ct)
    {
        var context = await _contexts.LoadAsync(userId, walletId, business, true, true, ct);
        // Serialize the submission claim, then commit it BEFORE the external
        // request. A second caller/crash recovery cannot start a second charge.
        await using var submission = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var submissionKey = "WalletCollectionInitiate:" + collectionId;
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({submissionKey}, 0))", ct);

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

        if (collection.PaymentMethod is not PaymentMethod.Card and not PaymentMethod.Interac)
            throw new InvalidOperationException("The saved collection method is not executable through this flow.");
        if (collection.Attempts.Count > 1 && collection.Attempts.OrderByDescending(x => x.AttemptedAt).First().Status == ProviderRequestStatus.Pending)
            throw new InvalidOperationException("A prior provider submission has an unknown outcome. Check payment status or contact support with this collection reference; a second charge was not started.");

        await _channels.ValidateInitiationAsync("Collection", collection.Id, ct);
        var compliance = await _contexts.EnsureComplianceAsync(context, ct);

        var email = request.PayerEmail ?? context.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Payer email is required to fund this wallet.");
        }

        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? context.Name
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
        await submission.CommitAsync(ct);

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
                    context.Phone,
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
            if (ex.ProviderStatusCode is not (400 or 401 or 403 or 404 or 422))
            {
                attempt.ErrorMessage = "Provider submission outcome is unknown. Reconcile this collection before retrying.";
                attempt.LastUpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(CancellationToken.None);
                throw; // Durable pending claim prevents unsafe resubmission after timeout/5xx.
            }
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

    public Task<CollectionDetailsDto> GetCollectionAsync(Guid userId, Guid walletId, Guid collectionId, CancellationToken ct = default) =>
        GetCoreAsync(userId, walletId, collectionId, false, ct);
    public Task<CollectionDetailsDto> GetBusinessCollectionAsync(Guid userId, Guid walletId, Guid collectionId, CancellationToken ct = default) =>
        GetCoreAsync(userId, walletId, collectionId, true, ct);
    private async Task<CollectionDetailsDto> GetCoreAsync(Guid userId, Guid walletId, Guid collectionId, bool business, CancellationToken ct)
    {
        await _contexts.LoadAsync(userId, walletId, business, false, false, ct);

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
                ChannelId = collection.PaymentChannelId, ChannelName = collection.PaymentChannelName,
                PaymentMethod = collection.PaymentMethod,
                Status = collection.Status,
                ProviderCode = collection.ProviderCode,
                ProviderCollectionId = collection.ProviderCollectionId,
                ProviderReference = collection.ProviderReference,
                FundingInstructions = FundingInstructionsFactory.ForCollection(collection),
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

}
