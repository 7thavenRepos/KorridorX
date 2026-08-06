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
        var sourceCountryCode = NormalizeCode(request.SourceCountryCode);
        var destinationCountryCode = NormalizeCode(request.DestinationCountryCode);
        var sourceCurrencyCode = NormalizeCode(request.SourceCurrencyCode);
        var destinationCurrencyCode = NormalizeCode(request.DestinationCurrencyCode);

        var customerProfile = await _db.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (customerProfile is null)
        {
            throw new InvalidOperationException("Customer profile not found.");
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
            SourceCurrencyCode = sourceCurrencyCode,
            DestinationCurrencyCode = destinationCurrencyCode,
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
                x.CustomerProfile.UserId == userId,
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
        var exists = await _db.CountryCurrencies
            .AsNoTracking()
            .AnyAsync(x =>
                x.CountryCode == countryCode &&
                x.CurrencyCode == currencyCode &&
                x.CanSend &&
                x.Country.IsSupported &&
                x.Country.IsSendCountry &&
                x.Currency.IsSupported,
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
        var exists = await _db.CountryCurrencies
            .AsNoTracking()
            .AnyAsync(x =>
                x.CountryCode == countryCode &&
                x.CurrencyCode == currencyCode &&
                x.CanReceive &&
                x.Country.IsSupported &&
                x.Country.IsReceiveCountry &&
                x.Currency.IsSupported,
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
            SourceCurrencyCode = quote.SourceCurrencyCode,
            DestinationCurrencyCode = quote.DestinationCurrencyCode,
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

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}