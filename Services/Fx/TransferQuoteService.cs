using KorridorX.Data;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Fx;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Fx;

public class TransferQuoteService : ITransferQuoteService
{
    private readonly AppDbContext _db;

    public TransferQuoteService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TransferQuoteDto> CreateQuoteAsync(
        Guid userId,
        CreateTransferQuoteRequestDto request,
        CancellationToken ct = default)
    {
        if (request.TransferType != KorridorX.Models.Enums.TransferType.ConsumerToConsumer)
        {
            throw new InvalidOperationException(
                "The consumer quote endpoint only supports ConsumerToConsumer transfers.");
        }

        if (request.SourceAmount <= 0)
        {
            throw new InvalidOperationException("Source amount must be greater than zero.");
        }

        var sourceCountryCode = NormalizeCountryCode(request.SourceCountryCode);
        var destinationCountryCode = NormalizeCountryCode(request.DestinationCountryCode);
        var sourceCurrencyCode = NormalizeAssetCode(request.SourceCurrencyCode);
        var destinationCurrencyCode = NormalizeAssetCode(request.DestinationCurrencyCode);

        var customerProfile = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        if (customerProfile is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
        }

        if (!string.Equals(
                customerProfile.CountryCode,
                sourceCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The source country must match the customer's profile country.");
        }

        await EnsureSendingCorridorAsync(sourceCountryCode, sourceCurrencyCode, ct);
        await EnsureReceivingCorridorAsync(destinationCountryCode, destinationCurrencyCode, ct);

        var exchangeRate = await _db.ExchangeRates
            .AsNoTracking()
            .Where(x =>
                x.SourceCurrencyCode == sourceCurrencyCode &&
                x.DestinationCurrencyCode == destinationCurrencyCode &&
                x.IsActive &&
                x.EffectiveFrom <= DateTime.UtcNow &&
                (x.EffectiveTo == null || x.EffectiveTo > DateTime.UtcNow))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        if (exchangeRate is null)
        {
            throw new InvalidOperationException("Exchange rate is not available for this currency pair.");
        }

        var fee = await _db.TransferFees
            .AsNoTracking()
            .Where(x =>
                x.SourceCountryCode == sourceCountryCode &&
                x.DestinationCountryCode == destinationCountryCode &&
                x.SourceCurrencyCode == sourceCurrencyCode &&
                x.DestinationCurrencyCode == destinationCurrencyCode &&
                x.TransferType == request.TransferType &&
                x.IsActive &&
                request.SourceAmount >= x.MinAmount &&
                (x.MaxAmount == null || request.SourceAmount <= x.MaxAmount))
            .OrderByDescending(x => x.MinAmount)
            .FirstOrDefaultAsync(ct);

        if (fee is null)
        {
            throw new InvalidOperationException("Transfer fee is not configured for this corridor.");
        }

        var feeAmount = fee.FixedFee + Math.Round(request.SourceAmount * fee.PercentageFee / 100m, 2);
        var totalPayableAmount = request.SourceAmount + feeAmount;
        var destinationAmount = Math.Round(request.SourceAmount * exchangeRate.CustomerRate, 2);

        var quote = new TransferQuote
        {
            CustomerProfileId = customerProfile.Id,
            SourceCountryCode = sourceCountryCode,
            DestinationCountryCode = destinationCountryCode,
            SourceCurrencyCode = sourceCurrencyCode,
            DestinationCurrencyCode = destinationCurrencyCode,
            TransferType = request.TransferType,
            SourceAmount = request.SourceAmount,
            DestinationAmount = destinationAmount,
            ProviderRate = exchangeRate.ProviderRate,
            CustomerRate = exchangeRate.CustomerRate,
            FeeAmount = feeAmount,
            FeeCurrencyCode = fee.FeeCurrencyCode,
            TotalPayableAmount = totalPayableAmount,
            ProviderCode = exchangeRate.ProviderCode,
            ProviderQuoteId = null,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false
        };

        _db.TransferQuotes.Add(quote);
        await _db.SaveChangesAsync(ct);

        return ToDto(quote);
    }

    public async Task<TransferQuoteDto> GetQuoteByIdAsync(
        Guid userId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var quote = await _db.TransferQuotes
            .AsNoTracking()
            .Include(x => x.CustomerProfile)
            .FirstOrDefaultAsync(x =>
                x.Id == quoteId &&
                x.CustomerProfileId != null &&
                x.CustomerProfile!.UserId == userId,
                ct);

        if (quote is null)
        {
            throw new InvalidOperationException("Quote not found.");
        }

        return ToDto(quote);
    }

    private async Task EnsureSendingCorridorAsync(
        string countryCode,
        string currencyCode,
        CancellationToken ct)
    {
        var exists = await _db.CountryAssets
            .AsNoTracking()
            .AnyAsync(x =>
                x.CountryCode == countryCode &&
                x.AssetCode == currencyCode &&
                x.CanSend &&
                x.Country.IsSupported &&
                x.Country.IsSendCountry &&
                x.Asset.IsSupported,
                ct);

        if (!exists)
        {
            throw new InvalidOperationException($"Currency '{currencyCode}' is not supported for sending from country '{countryCode}'.");
        }
    }

    private async Task EnsureReceivingCorridorAsync(
        string countryCode,
        string currencyCode,
        CancellationToken ct)
    {
        var exists = await _db.CountryAssets
            .AsNoTracking()
            .AnyAsync(x =>
                x.CountryCode == countryCode &&
                x.AssetCode == currencyCode &&
                x.CanReceive &&
                x.Country.IsSupported &&
                x.Country.IsReceiveCountry &&
                x.Asset.IsSupported,
                ct);

        if (!exists)
        {
            throw new InvalidOperationException($"Currency '{currencyCode}' is not supported for receiving in country '{countryCode}'.");
        }
    }

    private static TransferQuoteDto ToDto(TransferQuote quote)
    {
        return new TransferQuoteDto
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
    }

    private static string NormalizeCountryCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Country code is required.");

        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 10)
            throw new InvalidOperationException("Country code cannot exceed 10 characters.");

        return code;
    }

    private static string NormalizeAssetCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Asset code is required.");

        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 20)
            throw new InvalidOperationException("Asset code cannot exceed 20 characters.");

        return code;
    }
}