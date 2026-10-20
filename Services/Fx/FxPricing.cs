namespace KorridorX.Services.Fx;

public static class FxPricing
{
    public static void Validate(decimal markup, decimal? minimum, decimal? maximum)
    {
        if (markup is < 0m or >= 100m || decimal.Round(markup, 6) != markup)
            throw new InvalidOperationException("Markup must be at least zero and less than 100%, with at most six decimal places.");
        if (minimum is <= 0 || maximum is <= 0 || (minimum.HasValue && maximum.HasValue && minimum > maximum))
            throw new InvalidOperationException("Customer-rate limits must be positive, with minimum no greater than maximum.");
        foreach (var value in new[] { minimum, maximum })
            if (value.HasValue && (value > 9999999999.99999999m || decimal.Round(value.Value, 8) != value))
                throw new InvalidOperationException("Customer-rate limits support at most 10 whole digits and 8 decimal places.");
    }

    public static decimal Calculate(decimal providerRate, decimal markup, decimal? minimum, decimal? maximum)
    {
        Validate(markup, minimum, maximum);
        if (providerRate <= 0 || providerRate > 9999999999.99999999m || decimal.Round(providerRate, 8) != providerRate)
            throw new InvalidOperationException("The provider returned an invalid FX rate.");
        var result = providerRate * (1m - markup / 100m);
        if (minimum is decimal min && result < min) result = min;
        if (maximum is decimal max && result > max) result = max;
        result = decimal.Round(result, 8, MidpointRounding.AwayFromZero);
        if (result <= 0) throw new InvalidOperationException("The configured markup produces a zero customer rate.");
        return result;
    }
}
