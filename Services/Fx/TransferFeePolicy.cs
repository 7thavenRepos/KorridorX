using System.Globalization;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;

namespace KorridorX.Services.Fx;

public static class TransferFeePolicy
{
    // Existing fee amount columns are numeric(36,18).
    private const decimal MaximumAmount = 999999999999999999m;

    public static void Validate(SaveTransferFeeRequestDto request, int sourceDecimalPlaces)
    {
        if (!Enum.IsDefined(request.TransferType))
            throw new InvalidOperationException("Choose a supported transfer type.");
        if (request.MinAmount < 0 || request.MinAmount > MaximumAmount ||
            request.MaxAmount is decimal max && (max <= 0 || max < request.MinAmount || max > MaximumAmount))
            throw new InvalidOperationException("Use a nonnegative minimum and an optional positive maximum no smaller than the minimum.");
        if (request.FixedFee < 0 || request.FixedFee > MaximumAmount || request.PercentageFee < 0 || request.PercentageFee > 100)
            throw new InvalidOperationException("The fixed fee must be nonnegative and the percentage must be between 0 and 100.");
        var precision = Math.Clamp(sourceDecimalPlaces, 0, 18);
        if (decimal.Round(request.MinAmount, precision) != request.MinAmount ||
            request.MaxAmount is decimal upper && decimal.Round(upper, precision) != upper ||
            decimal.Round(request.FixedFee, precision) != request.FixedFee)
            throw new InvalidOperationException($"The minimum, maximum and fixed fee must use at most {precision} decimal places for the source currency.");
        if (decimal.Round(request.PercentageFee, 6) != request.PercentageFee)
            throw new InvalidOperationException("The percentage fee supports at most 6 decimal places.");
    }

    // The fee belongs to the funding wallet's currency direction, transfer type
    // and amount band. Country of residence and recipient country remain separate
    // eligibility/compliance inputs and must not participate in fee selection.
    public static IQueryable<TransferFee> MatchingRules(
        IQueryable<TransferFee> fees, string sourceCurrency, string destinationCurrency,
        TransferType transferType, decimal amount) =>
        fees.Where(x => !x.IsDeleted && x.IsActive &&
            x.SourceCurrencyCode == sourceCurrency && x.DestinationCurrencyCode == destinationCurrency &&
            x.TransferType == transferType && amount >= x.MinAmount &&
            (x.MaxAmount == null || amount <= x.MaxAmount));

    public static bool Overlaps(decimal minimum, decimal? maximum, decimal otherMinimum, decimal? otherMaximum) =>
        (!maximum.HasValue || otherMinimum <= maximum.Value) && (!otherMaximum.HasValue || minimum <= otherMaximum.Value);

    public static string Revision(TransferFee fee) =>
        (fee.LastUpdatedAt ?? fee.CreatedAt).Ticks.ToString(CultureInfo.InvariantCulture);

    public static void RequireRevision(TransferFee fee, string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision) || Revision(fee) != revision)
            throw new InvalidOperationException("This fee rule changed. Reload transfer fees before editing or activating it.");
    }

    public static DateTime NextUpdate(TransferFee fee)
    {
        // PostgreSQL timestamps retain microseconds. Returning the same precision keeps
        // the revision stable after a request reload and monotonically increases it.
        var ticks = DateTime.UtcNow.Ticks;
        var previous = (fee.LastUpdatedAt ?? fee.CreatedAt).Ticks;
        ticks -= ticks % 10;
        if (ticks <= previous) ticks = previous - previous % 10 + 10;
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
