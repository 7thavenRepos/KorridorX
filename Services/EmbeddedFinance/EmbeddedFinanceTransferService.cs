using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Fx;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Services.Compliance;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Payments;
using KorridorX.Services.References;
using KorridorX.Services.Transfers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceTransferService : IEmbeddedFinanceTransferService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedFinanceContextAccessor _context;
    private readonly IFinancialReservationService _reservations;
    private readonly ITransferStatusService _transferStatus;
    private readonly IComplianceLimitService _limits;
    private readonly IPayoutService _payouts;
    private readonly IReferenceGenerator _references;
    private readonly IEmbeddedWebhookPublisher _webhooks;

    public EmbeddedFinanceTransferService(
        AppDbContext db,
        IEmbeddedFinanceContextAccessor context,
        IFinancialReservationService reservations,
        ITransferStatusService transferStatus,
        IComplianceLimitService limits,
        IPayoutService payouts,
        IReferenceGenerator references,
        IEmbeddedWebhookPublisher webhooks)
    {
        _db = db;
        _context = context;
        _reservations = reservations;
        _transferStatus = transferStatus;
        _limits = limits;
        _payouts = payouts;
        _references = references;
        _webhooks = webhooks;
    }

    public async Task<TransferQuoteDto> CreateQuoteAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CreateEmbeddedTransferQuoteRequestDto request,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TransfersWrite);
        var customer = await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        var account = await GetSourceCollectionAccountAsync(
            principal.BusinessProfileId,
            businessCustomerId,
            collectionAccountId,
            ct);

        if (customer.Status != BusinessCustomerStatus.Active)
            throw new InvalidOperationException("Business customer must be active.");
        if (account.Status != CollectionAccountStatus.Active)
            throw new InvalidOperationException("Collection account must be active.");
        if (account.FinancialAccount.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Source Financial Account must be active.");

        if (request.TransferType is not TransferType.BusinessToConsumer and not TransferType.BusinessToBusiness)
            throw new InvalidOperationException(
                "Embedded transfer quotes support BusinessToConsumer and BusinessToBusiness transfers.");

        if (request.SourceAmount <= 0m)
            throw new InvalidOperationException("Source amount must be greater than zero.");

        var sourceCountry = NormalizeCountry(customer.CountryCode);
        var destinationCountry = NormalizeCountry(request.DestinationCountryCode);
        var sourceCurrency = NormalizeAsset(account.AssetCode);
        var destinationCurrency = NormalizeAsset(request.DestinationCurrencyCode);

        if (string.Equals(sourceCurrency, destinationCurrency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Use the embedded payout endpoint for same-currency money movement.");

        await EnsureCorridorAsync(sourceCountry, sourceCurrency, sending: true, ct);
        await EnsureCorridorAsync(destinationCountry, destinationCurrency, sending: false, ct);

        var rate = await _db.ExchangeRates.AsNoTracking()
            .Where(x =>
                x.SourceCurrencyCode == sourceCurrency &&
                x.DestinationCurrencyCode == destinationCurrency &&
                x.IsActive &&
                x.EffectiveFrom <= DateTime.UtcNow &&
                (x.EffectiveTo == null || x.EffectiveTo > DateTime.UtcNow))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Exchange rate is not available for this currency pair.");

        var fee = await _db.TransferFees.AsNoTracking()
            .Where(x =>
                x.SourceCountryCode == sourceCountry &&
                x.DestinationCountryCode == destinationCountry &&
                x.SourceCurrencyCode == sourceCurrency &&
                x.DestinationCurrencyCode == destinationCurrency &&
                x.TransferType == request.TransferType &&
                x.IsActive &&
                request.SourceAmount >= x.MinAmount &&
                (x.MaxAmount == null || request.SourceAmount <= x.MaxAmount))
            .OrderByDescending(x => x.MinAmount)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Transfer fee is not configured for this corridor.");

        var feeAmount = Math.Round(
            fee.FixedFee + (request.SourceAmount * fee.PercentageFee / 100m),
            2,
            MidpointRounding.AwayFromZero);

        var quote = new TransferQuote
        {
            BusinessProfileId = principal.BusinessProfileId,
            BusinessCustomerId = businessCustomerId,
            SourceFinancialAccountId = account.FinancialAccountId,
            SourceCountryCode = sourceCountry,
            DestinationCountryCode = destinationCountry,
            SourceCurrencyCode = sourceCurrency,
            DestinationCurrencyCode = destinationCurrency,
            TransferType = request.TransferType,
            SourceAmount = request.SourceAmount,
            DestinationAmount = Math.Round(
                request.SourceAmount * rate.CustomerRate,
                2,
                MidpointRounding.AwayFromZero),
            ProviderRate = rate.ProviderRate,
            CustomerRate = rate.CustomerRate,
            FeeAmount = feeAmount,
            FeeCurrencyCode = fee.FeeCurrencyCode,
            TotalPayableAmount = request.SourceAmount + feeAmount,
            ProviderCode = rate.ProviderCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false
        };

        _db.TransferQuotes.Add(quote);
        await _db.SaveChangesAsync(ct);
        return ToQuoteDto(quote);
    }

    public async Task<TransferQuoteDto> GetQuoteAsync(
        Guid businessCustomerId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TransfersRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);

        var quote = await _db.TransferQuotes.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == quoteId &&
            x.BusinessProfileId == principal.BusinessProfileId &&
            x.BusinessCustomerId == businessCustomerId &&
            !x.IsDeleted,
            ct)
            ?? throw new InvalidOperationException("Embedded transfer quote not found.");

        return ToQuoteDto(quote);
    }

    public async Task<PagedResult<EmbeddedTransferDto>> GetTransfersAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TransfersRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);

        var paged = await _db.Transfers.AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.SourceFinancialAccountId != null &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<EmbeddedTransferDto>
        {
            Items = paged.Items.Select(ToDto).ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<EmbeddedTransferDto> GetTransferAsync(
        Guid businessCustomerId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TransfersRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);

        var transfer = await _db.Transfers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == transferId &&
            x.BusinessProfileId == principal.BusinessProfileId &&
            x.BusinessCustomerId == businessCustomerId &&
            x.SourceFinancialAccountId != null &&
            !x.IsDeleted,
            ct)
            ?? throw new InvalidOperationException("Embedded transfer not found.");

        return ToDto(transfer);
    }

    public async Task<EmbeddedTransferCreateResultDto> CreateTransferAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CreateEmbeddedTransferRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.TransfersWrite);
        var key = NormalizeKey(idempotencyKey);
        var externalReference = NormalizeReference(request.ExternalReference);

        if (!Enum.IsDefined(typeof(TransferPurpose), request.Purpose))
            throw new InvalidOperationException("A valid transfer purpose is required.");

        var requestHash = Hash(new
        {
            BusinessCustomerId = businessCustomerId,
            CollectionAccountId = collectionAccountId,
            request.TransferQuoteId,
            request.BusinessBeneficiaryId,
            request.BusinessBeneficiaryBankAccountId,
            request.Purpose,
            PurposeNote = Clean(request.PurposeNote, 500),
            ExternalReference = externalReference
        });

        var existingIdem = await _db.EmbeddedApiIdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApiApplicationId == principal.ApiApplicationId &&
                x.IdempotencyKey == key &&
                !x.IsDeleted,
                ct);

        if (existingIdem is not null)
        {
            if (!string.Equals(existingIdem.ResourceType, nameof(Transfer), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The Idempotency-Key has already been used for a different operation.");

            if (!string.Equals(existingIdem.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "The Idempotency-Key has already been used with a different request.");

            var existing = await _db.Transfers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == existingIdem.ResourceId &&
                    x.BusinessProfileId == principal.BusinessProfileId &&
                    x.BusinessCustomerId == businessCustomerId &&
                    !x.IsDeleted,
                    ct)
                ?? throw new InvalidOperationException(
                    "Idempotent embedded transfer resource was not found.");

            return new EmbeddedTransferCreateResultDto(ToDto(existing), false);
        }

        var customer = await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        if (customer.Status != BusinessCustomerStatus.Active)
            throw new InvalidOperationException("Business customer must be active.");

        var externalReferenceExists = await _db.Transfers.AsNoTracking().AnyAsync(x =>
            x.BusinessCustomerId == businessCustomerId &&
            x.ExternalReference == externalReference &&
            !x.IsDeleted,
            ct);

        if (externalReferenceExists)
            throw new InvalidOperationException(
                "External reference has already been used for this business customer.");

        var collectionAccount = await GetSourceCollectionAccountAsync(
            principal.BusinessProfileId,
            businessCustomerId,
            collectionAccountId,
            ct);

        if (collectionAccount.Status != CollectionAccountStatus.Active)
            throw new InvalidOperationException("Collection account must be active.");

        var financialAccount = collectionAccount.FinancialAccount;
        if (financialAccount.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Source Financial Account must be active.");

        var quote = await _db.TransferQuotes.FirstOrDefaultAsync(x =>
            x.Id == request.TransferQuoteId &&
            x.BusinessProfileId == principal.BusinessProfileId &&
            x.BusinessCustomerId == businessCustomerId &&
            x.SourceFinancialAccountId == financialAccount.Id &&
            !x.IsDeleted,
            ct)
            ?? throw new InvalidOperationException("Embedded transfer quote not found.");

        if (quote.IsUsed)
            throw new InvalidOperationException("Transfer quote has already been used.");
        if (quote.IsExpired)
            throw new InvalidOperationException("Transfer quote has expired. Create a new quote.");
        if (!string.Equals(quote.SourceCurrencyCode, financialAccount.AssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Transfer quote source asset no longer matches the source account.");

        var beneficiary = await _db.BusinessBeneficiaries
            .Include(x => x.BankAccounts)
            .FirstOrDefaultAsync(x =>
                x.Id == request.BusinessBeneficiaryId &&
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.IsActive &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business beneficiary not found.");

        var bankAccount = beneficiary.BankAccounts.FirstOrDefault(x =>
            x.Id == request.BusinessBeneficiaryBankAccountId &&
            x.IsActive &&
            !x.IsDeleted)
            ?? throw new InvalidOperationException("Business beneficiary bank account not found.");

        ValidateBeneficiaryType(quote.TransferType, beneficiary.BeneficiaryType);

        if (!string.Equals(beneficiary.CountryCode, quote.DestinationCountryCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Beneficiary country does not match the quote destination country.");
        if (!string.Equals(bankAccount.CountryCode, quote.DestinationCountryCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(bankAccount.CurrencyCode, quote.DestinationCurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Beneficiary bank account is not valid for the quoted destination.");

        await EnsureEmbeddedProviderReadinessAsync(
            businessCustomerId,
            quote.ProviderCode,
            bankAccount,
            ct);

        var transfer = new Transfer
        {
            Reference = _references.GenerateTransferReference(),
            ExternalReference = externalReference,
            BusinessProfileId = principal.BusinessProfileId,
            BusinessCustomerId = businessCustomerId,
            SourceFinancialAccountId = financialAccount.Id,
            BusinessBeneficiaryId = beneficiary.Id,
            BusinessBeneficiaryBankAccountId = bankAccount.Id,
            TransferQuoteId = quote.Id,
            TransferType = quote.TransferType,
            Purpose = request.Purpose,
            PurposeNote = Clean(request.PurposeNote, 500),
            SourceCountryCode = quote.SourceCountryCode,
            DestinationCountryCode = quote.DestinationCountryCode,
            SourceCurrencyCode = quote.SourceCurrencyCode,
            DestinationCurrencyCode = quote.DestinationCurrencyCode,
            SourceAmount = quote.SourceAmount,
            DestinationAmount = quote.DestinationAmount,
            FeeAmount = quote.FeeAmount,
            FeeCurrencyCode = quote.FeeCurrencyCode,
            TotalPayableAmount = quote.TotalPayableAmount,
            CustomerRate = quote.CustomerRate,
            ProviderRate = quote.ProviderRate,
            ProviderCode = quote.ProviderCode,
            ApprovalStatus = BusinessApprovalStatus.NotRequired,
            RequiredApprovals = 0,
            Status = TransferStatus.Draft
        };

        await _limits.EnsureWithinLimitsAsync(transfer, ct);

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _db.Transfers.Add(transfer);
            _db.EmbeddedApiIdempotencyRecords.Add(new EmbeddedApiIdempotencyRecord
            {
                ApiApplicationId = principal.ApiApplicationId,
                IdempotencyKey = key,
                RequestHash = requestHash,
                ResourceType = nameof(Transfer),
                ResourceId = transfer.Id
            });

            _transferStatus.ApplyTransition(
                transfer,
                TransferStatus.PendingPayment,
                new TransferStatusTransitionContext(
                    "EmbeddedApi",
                    "Embedded transfer created and awaiting account reservation.",
                    null,
                    EventType: "EMBEDDED_TRANSFER_CREATED",
                    Title: "Embedded transfer created",
                    Description: "The transfer was created successfully."));

            await _reservations.ReserveAsync(
                financialAccount.Id,
                FinancialReservationType.Transfer,
                nameof(Transfer),
                transfer.Id,
                quote.TotalPayableAmount,
                null,
                nameof(BusinessCustomer),
                businessCustomerId,
                ct);

            _transferStatus.ApplyTransition(
                transfer,
                TransferStatus.PaymentReceived,
                new TransferStatusTransitionContext(
                    "EmbeddedApi",
                    "Funds were reserved from the embedded customer account.",
                    null,
                    EventType: "EMBEDDED_ACCOUNT_FUNDED",
                    Title: "Transfer funded",
                    Description: "Funds were reserved from the embedded customer account."));

            quote.IsUsed = true;
            quote.UsedAt = DateTime.UtcNow;
            quote.LastUpdatedAt = DateTime.UtcNow;

            await _webhooks.PublishAsync(
                principal.BusinessProfileId,
                "transfer.created",
                new
                {
                    id = transfer.Id,
                    businessCustomerId,
                    sourceFinancialAccountId = financialAccount.Id,
                    transfer.Reference,
                    transfer.ExternalReference,
                    sourceAmount = transfer.SourceAmount,
                    sourceCurrencyCode = transfer.SourceCurrencyCode,
                    destinationAmount = transfer.DestinationAmount,
                    destinationCurrencyCode = transfer.DestinationCurrencyCode,
                    status = transfer.Status.ToString()
                },
                ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var recovered = await RecoverConcurrentIdempotentTransferAsync(
                principal.ApiApplicationId,
                principal.BusinessProfileId,
                businessCustomerId,
                key,
                requestHash,
                ct);
            if (recovered is not null) return recovered;
            throw;
        }

        var dispatched = false;
        try
        {
            await _payouts.DispatchForTransferAsync(
                transfer.Id,
                "EmbeddedApi",
                null,
                ct);
            dispatched = true;
        }
        catch
        {
            // The canonical payout flow records its own provider failure state.
            // Preserve the created transfer so API callers can query/retry operationally.
        }

        var fresh = await _db.Transfers.AsNoTracking()
            .FirstAsync(x => x.Id == transfer.Id, ct);

        return new EmbeddedTransferCreateResultDto(ToDto(fresh), dispatched);
    }

    private async Task<EmbeddedTransferCreateResultDto?> RecoverConcurrentIdempotentTransferAsync(
        Guid apiApplicationId,
        Guid businessProfileId,
        Guid businessCustomerId,
        string idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        var idem = await _db.EmbeddedApiIdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApiApplicationId == apiApplicationId &&
                x.IdempotencyKey == idempotencyKey &&
                !x.IsDeleted,
                ct);

        if (idem is null) return null;
        if (!string.Equals(idem.ResourceType, nameof(Transfer), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The Idempotency-Key has already been used for a different operation.");
        if (!string.Equals(idem.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The Idempotency-Key has already been used with a different request.");

        var existing = await _db.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == idem.ResourceId &&
                x.BusinessProfileId == businessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Idempotent embedded transfer resource was not found after concurrent creation.");

        return new EmbeddedTransferCreateResultDto(ToDto(existing), false);
    }

    private EmbeddedFinancePrincipal RequireScope(EmbeddedFinanceScope scope)
    {
        var principal = _context.GetRequiredPrincipal();
        if (!principal.HasScope(scope))
            throw new UnauthorizedAccessException(
                $"API application does not have required scope '{scope}'.");
        return principal;
    }

    private async Task<BusinessCustomer> EnsureCustomerAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        CancellationToken ct) =>
        await _db.BusinessCustomers.FirstOrDefaultAsync(x =>
            x.Id == businessCustomerId &&
            x.BusinessProfileId == businessProfileId &&
            !x.IsDeleted,
            ct)
        ?? throw new InvalidOperationException("Business customer not found.");

    private async Task<CollectionAccount> GetSourceCollectionAccountAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        Guid collectionAccountId,
        CancellationToken ct)
    {
        var account = await _db.CollectionAccounts
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionAccountId &&
                x.BusinessProfileId == businessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Collection account not found.");

        if (account.FinancialAccount.OwnerType != FinancialAccountOwnerType.BusinessCustomer ||
            account.FinancialAccount.OwnerId != businessCustomerId)
            throw new InvalidOperationException("Collection account Financial Account ownership is invalid.");

        if (!string.Equals(account.AssetCode, account.FinancialAccount.AssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Collection account asset does not match its Financial Account asset.");

        return account;
    }

    private static void ValidateBeneficiaryType(TransferType transferType, BusinessBeneficiaryType beneficiaryType)
    {
        var typeName = beneficiaryType.ToString();
        if (transferType == TransferType.BusinessToConsumer && !typeName.Equals("Individual", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("BusinessToConsumer transfers require an individual beneficiary.");
        if (transferType == TransferType.BusinessToBusiness && !typeName.Equals("Business", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("BusinessToBusiness transfers require a business beneficiary.");
    }

    private async Task EnsureEmbeddedProviderReadinessAsync(
        Guid businessCustomerId,
        string providerCodeText,
        KorridorX.Models.BusinessBeneficiaries.BusinessBeneficiaryBankAccount bankAccount,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ProviderCode>(providerCodeText, true, out var providerCode))
            throw new InvalidOperationException($"Provider '{providerCodeText}' is not supported for embedded transfers.");

        var providerCustomer = await _db.ProviderCustomers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ProviderCode == providerCode && x.BusinessCustomerId == businessCustomerId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Embedded business customer has not completed provider onboarding for transfers.");

        if (string.IsNullOrWhiteSpace(providerCustomer.ProviderCustomerId) || IsBlockedProviderStatus(providerCustomer.ProviderStatus))
            throw new InvalidOperationException("Embedded business customer is not ready for payout with the selected provider.");

        var mapping = await _db.PayoutDestinationProviderMappings.AsNoTracking().FirstOrDefaultAsync(x =>
            x.DestinationType == PayoutDestinationType.BusinessBeneficiaryBankAccount &&
            x.DestinationId == bankAccount.Id &&
            x.ProviderCode == providerCode && x.IsActive && !x.IsDeleted, ct);

        var verified = mapping is not null ? mapping.IsVerified : bankAccount.IsVerified;
        var providerBankId = mapping?.ProviderBankId ?? bankAccount.ProviderBankId;
        var verifiedName = mapping?.ProviderVerifiedAccountName ?? bankAccount.ProviderVerifiedAccountName;
        if (!verified || string.IsNullOrWhiteSpace(providerBankId) || string.IsNullOrWhiteSpace(verifiedName))
            throw new InvalidOperationException("Business beneficiary bank account is not verified for the selected payout provider.");
    }

    private static bool IsBlockedProviderStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Trim().ToUpperInvariant() is "FAILED" or "REJECTED" or "SUSPENDED" or "DISABLED" or "CLOSED" or "BLOCKED";
    }

    private async Task EnsureCorridorAsync(
        string countryCode,
        string assetCode,
        bool sending,
        CancellationToken ct)
    {
        var exists = await _db.CountryAssets.AsNoTracking().AnyAsync(x =>
            x.CountryCode == countryCode &&
            x.AssetCode == assetCode &&
            (sending ? x.CanSend : x.CanReceive) &&
            x.Country.IsSupported &&
            (sending ? x.Country.IsSendCountry : x.Country.IsReceiveCountry) &&
            x.Asset.IsSupported,
            ct);

        if (!exists)
            throw new InvalidOperationException(
                $"Asset '{assetCode}' is not enabled for {(sending ? "sending" : "receiving")} in '{countryCode}'.");
    }

    private static TransferQuoteDto ToQuoteDto(TransferQuote quote) => new()
    {
        Id = quote.Id,
        CustomerProfileId = quote.CustomerProfileId,
        BusinessProfileId = quote.BusinessProfileId,
        BusinessCustomerId = quote.BusinessCustomerId,
        SourceFinancialAccountId = quote.SourceFinancialAccountId,
        SourceCountryCode = quote.SourceCountryCode,
        DestinationCountryCode = quote.DestinationCountryCode,
        SourceCurrencyCode = quote.SourceCurrencyCode,
        DestinationCurrencyCode = quote.DestinationCurrencyCode,
        TransferType = quote.TransferType,
        SourceAmount = quote.SourceAmount,
        DestinationAmount = quote.DestinationAmount,
        ProviderRate = quote.ProviderRate,
        CustomerRate = quote.CustomerRate,
        FeeAmount = quote.FeeAmount,
        FeeCurrencyCode = quote.FeeCurrencyCode,
        TotalPayableAmount = quote.TotalPayableAmount,
        ProviderCode = quote.ProviderCode,
        ProviderQuoteId = quote.ProviderQuoteId,
        ExpiresAt = quote.ExpiresAt,
        IsUsed = quote.IsUsed,
        IsExpired = quote.IsExpired,
        CreatedAt = quote.CreatedAt
    };

    private static EmbeddedTransferDto ToDto(Transfer x) =>
        new(
            x.Id,
            x.Reference,
            x.ExternalReference,
            x.BusinessCustomerId ?? Guid.Empty,
            x.SourceFinancialAccountId ?? Guid.Empty,
            x.TransferQuoteId,
            x.BusinessBeneficiaryId ?? Guid.Empty,
            x.BusinessBeneficiaryBankAccountId,
            x.TransferType,
            x.Purpose,
            x.PurposeNote,
            x.SourceCountryCode,
            x.DestinationCountryCode,
            x.SourceCurrencyCode,
            x.DestinationCurrencyCode,
            x.SourceAmount,
            x.DestinationAmount,
            x.FeeAmount,
            x.TotalPayableAmount,
            x.CustomerRate,
            x.Status,
            x.ProviderCode,
            x.ProviderTransferId,
            x.ProviderReference,
            x.FailureReason,
            x.CreatedAt,
            x.PaymentReceivedAt,
            x.PayoutInitiatedAt,
            x.CompletedAt,
            x.FailedAt);

    private static string NormalizeKey(string value)
    {
        var result = (value ?? "").Trim();
        if (result.Length == 0)
            throw new InvalidOperationException("Idempotency-Key header is required.");
        if (result.Length > 200)
            throw new InvalidOperationException("Idempotency-Key cannot exceed 200 characters.");
        return result;
    }

    private static string NormalizeReference(string value)
    {
        var result = (value ?? "").Trim();
        if (result.Length == 0)
            throw new InvalidOperationException("External reference is required.");
        if (result.Length > 50)
            throw new InvalidOperationException("External reference cannot exceed 50 characters.");
        return result;
    }

    private static string NormalizeCountry(string value)
    {
        var result = (value ?? "").Trim().ToUpperInvariant();
        if (result.Length == 0)
            throw new InvalidOperationException("Country code is required.");
        if (result.Length > 10)
            throw new InvalidOperationException("Country code cannot exceed 10 characters.");
        return result;
    }

    private static string NormalizeAsset(string value)
    {
        var result = (value ?? "").Trim().ToUpperInvariant();
        if (result.Length == 0)
            throw new InvalidOperationException("Asset code is required.");
        if (result.Length > 20)
            throw new InvalidOperationException("Asset code cannot exceed 20 characters.");
        return result;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var result = value.Trim();
        return result.Length <= maxLength ? result : result[..maxLength];
    }

    private static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
