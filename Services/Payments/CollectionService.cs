using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Payments;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;
using KorridorX.Services.References;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Payments;

public class CollectionService : ICollectionService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ICollectionPaymentMethodPolicy _paymentMethodPolicy;

    public CollectionService(
        AppDbContext db,
        IReferenceGenerator referenceGenerator,
        ICollectionPaymentMethodPolicy paymentMethodPolicy)
    {
        _db = db;
        _referenceGenerator = referenceGenerator;
        _paymentMethodPolicy = paymentMethodPolicy;
    }

    public async Task<IReadOnlyList<CollectionPaymentMethodDto>> GetAvailablePaymentMethodsAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var transfer = await GetOwnedTransferAsync(userId, transferId, false, ct);

        if (transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"Payment methods are not available while the transfer status is '{transfer.Status}'.");
        }

        var methods = _paymentMethodPolicy.GetSupportedMethods(
            transfer.SourceCountryCode,
            transfer.SourceCurrencyCode);

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"No collection payment methods are configured for {transfer.SourceCountryCode}/{transfer.SourceCurrencyCode}.");
        }

        return methods
            .Select(ToPaymentMethodDto)
            .ToList();
    }

    public async Task<CollectionDetailsDto> CreateCollectionAsync(
        Guid userId,
        Guid transferId,
        CreateCollectionRequestDto request,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var transfer = await GetOwnedTransferAsync(userId, transferId, true, ct);

        var existingCollection = await _db.Collections
            .Include(x => x.Transfer)
            .ThenInclude(x => x.CustomerProfile)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.TransferId == transfer.Id &&
                !x.IsDeleted,
                ct);

        if (existingCollection is not null)
        {
            if (existingCollection.PaymentMethod != request.PaymentMethod)
            {
                throw new InvalidOperationException(
                    $"A collection already exists for this transfer using '{GetPaymentMethodDisplayName(existingCollection.PaymentMethod)}'.");
            }

            await tx.CommitAsync(ct);
            return ToDetailsDto(existingCollection);
        }

        if (transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"A collection cannot be created while the transfer status is '{transfer.Status}'.");
        }

        if (!_paymentMethodPolicy.IsSupported(
                transfer.SourceCountryCode,
                transfer.SourceCurrencyCode,
                request.PaymentMethod))
        {
            throw new InvalidOperationException(
                $"Payment method '{GetPaymentMethodDisplayName(request.PaymentMethod)}' is not supported for {transfer.SourceCountryCode}/{transfer.SourceCurrencyCode}.");
        }

        var now = DateTime.UtcNow;
        var reference = await GenerateUniqueCollectionReferenceAsync(ct);

        var collection = new Collection
        {
            TransferId = transfer.Id,
            Transfer = transfer,
            Reference = reference,
            CurrencyCode = transfer.SourceCurrencyCode,
            Amount = transfer.TotalPayableAmount,
            PaymentMethod = request.PaymentMethod,
            Status = CollectionStatus.Pending,
            ProviderCode = transfer.ProviderCode,
            CreatedByUserId = userId
        };

        var attempt = new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = JsonSerializer.Serialize(new
            {
                transferId = transfer.Id,
                transferReference = transfer.Reference,
                collectionReference = reference,
                amount = transfer.TotalPayableAmount,
                currencyCode = transfer.SourceCurrencyCode,
                paymentMethod = request.PaymentMethod.ToString()
            }),
            AttemptedAt = now
        };

        collection.Attempts.Add(attempt);

        _db.Collections.Add(collection);
        _db.TransferTimelineEvents.Add(new TransferTimelineEvent
        {
            TransferId = transfer.Id,
            EventType = "COLLECTION_CREATED",
            Title = "Payment method selected",
            Description = $"{GetPaymentMethodDisplayName(request.PaymentMethod)} was selected to fund this transfer.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                collectionId = collection.Id,
                collectionReference = collection.Reference,
                paymentMethod = request.PaymentMethod.ToString()
            }),
            OccurredAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDetailsDto(collection);
    }

    public async Task<CollectionDetailsDto> GetCollectionByIdAsync(
        Guid userId,
        Guid collectionId,
        CancellationToken ct = default)
    {
        var collection = await BuildOwnedCollectionQuery(userId)
            .FirstOrDefaultAsync(x => x.Id == collectionId, ct);

        if (collection is null)
        {
            throw new InvalidOperationException("Collection not found.");
        }

        return ToDetailsDto(collection);
    }

    public async Task<CollectionDetailsDto> GetTransferCollectionAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var collection = await BuildOwnedCollectionQuery(userId)
            .FirstOrDefaultAsync(x => x.TransferId == transferId, ct);

        if (collection is null)
        {
            throw new InvalidOperationException("No collection has been created for this transfer.");
        }

        return ToDetailsDto(collection);
    }

    public async Task<PagedResult<CollectionDto>> GetMyCollectionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return await _db.Collections
            .AsNoTracking()
            .Where(x =>
                x.Transfer.CustomerProfile.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CollectionDto(
                x.Id,
                x.TransferId,
                x.Transfer.Reference,
                x.Transfer.Status,
                x.Reference,
                x.Transfer.SourceCountryCode,
                x.CurrencyCode,
                x.Amount,
                x.PaymentMethod,
                x.Status,
                x.ProviderCode,
                x.ProviderCollectionId,
                x.ProviderReference,
                x.CheckoutUrl,
                x.VirtualAccountNumber,
                x.VirtualAccountBankName,
                x.VirtualAccountName,
                x.InitiatedAt,
                x.ConfirmedAt,
                x.FailedAt,
                x.FailureReason,
                x.CreatedAt,
                x.LastUpdatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    private async Task<Transfer> GetOwnedTransferAsync(
        Guid userId,
        Guid transferId,
        bool tracked,
        CancellationToken ct)
    {
        IQueryable<Transfer> query = _db.Transfers
            .Include(x => x.CustomerProfile);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        var transfer = await query.FirstOrDefaultAsync(x =>
            x.Id == transferId &&
            x.CustomerProfile.UserId == userId &&
            !x.IsDeleted &&
            !x.CustomerProfile.IsDeleted,
            ct);

        if (transfer is null)
        {
            throw new InvalidOperationException("Transfer not found.");
        }

        return transfer;
    }

    private IQueryable<Collection> BuildOwnedCollectionQuery(Guid userId)
    {
        return _db.Collections
            .AsNoTracking()
            .Include(x => x.Transfer)
            .ThenInclude(x => x.CustomerProfile)
            .Include(x => x.Attempts)
            .Where(x =>
                x.Transfer.CustomerProfile.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile.IsDeleted);
    }

    private async Task<string> GenerateUniqueCollectionReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateCollectionReference();

            var exists = await _db.Collections
                .AsNoTracking()
                .AnyAsync(x => x.Reference == reference, ct);

            if (!exists)
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique collection reference.");
    }

    private static CollectionDetailsDto ToDetailsDto(Collection collection)
    {
        return new CollectionDetailsDto(
            ToDto(collection),
            collection.Attempts
                .OrderByDescending(x => x.AttemptedAt)
                .Select(ToAttemptDto)
                .ToList());
    }

    private static CollectionDto ToDto(Collection collection)
    {
        return new CollectionDto(
            collection.Id,
            collection.TransferId,
            collection.Transfer.Reference,
            collection.Transfer.Status,
            collection.Reference,
            collection.Transfer.SourceCountryCode,
            collection.CurrencyCode,
            collection.Amount,
            collection.PaymentMethod,
            collection.Status,
            collection.ProviderCode,
            collection.ProviderCollectionId,
            collection.ProviderReference,
            collection.CheckoutUrl,
            collection.VirtualAccountNumber,
            collection.VirtualAccountBankName,
            collection.VirtualAccountName,
            collection.InitiatedAt,
            collection.ConfirmedAt,
            collection.FailedAt,
            collection.FailureReason,
            collection.CreatedAt,
            collection.LastUpdatedAt);
    }

    private static CollectionAttemptDto ToAttemptDto(CollectionAttempt attempt)
    {
        return new CollectionAttemptDto(
            attempt.Id,
            attempt.CollectionId,
            attempt.Status,
            attempt.ProviderRequestId,
            attempt.ProviderResponseId,
            attempt.RequestPayloadJson,
            attempt.ResponsePayloadJson,
            attempt.AttemptedAt,
            attempt.ErrorMessage);
    }

    private static CollectionPaymentMethodDto ToPaymentMethodDto(PaymentMethod paymentMethod)
    {
        return new CollectionPaymentMethodDto(
            paymentMethod,
            paymentMethod.ToString(),
            GetPaymentMethodDisplayName(paymentMethod));
    }

    private static string GetPaymentMethodDisplayName(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Ach => "ACH",
            PaymentMethod.Sepa => "SEPA",
            PaymentMethod.Interac => "Interac",
            PaymentMethod.BankTransfer => "Bank transfer",
            PaymentMethod.MobileMoney => "Mobile money",
            PaymentMethod.VirtualAccount => "Virtual account",
            _ => paymentMethod.ToString()
        };
    }
}
