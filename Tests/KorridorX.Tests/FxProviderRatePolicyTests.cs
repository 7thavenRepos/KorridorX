using KorridorX.Dtos.Fx;
using KorridorX.Services.Fx;

namespace KorridorX.Tests;

public sealed class FxProviderRatePolicyTests
{
    [Fact]
    public void Percentage_markup_is_applied_once_in_the_customer_direction()
    {
        Assert.Equal(980m, FxPricing.Calculate(1000m, 2m, null, null));
        Assert.Equal(0.98m, FxPricing.Calculate(1m, 2m, null, null));
        Assert.Equal(990m, FxPricing.Calculate(1000m, 2m, 990m, null));
        Assert.Equal(970m, FxPricing.Calculate(1000m, 2m, null, 970m));
    }

    [Theory]
    [InlineData(-1)] [InlineData(100)] [InlineData(101)]
    public void Invalid_markup_is_rejected(decimal markup) =>
        Assert.Throws<InvalidOperationException>(() => FxPricing.Calculate(100m, markup, null, null));

    [Fact]
    public void Invalid_limits_precision_and_zero_rates_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => FxPricing.Validate(1m, 2m, 1m));
        Assert.Throws<InvalidOperationException>(() => FxPricing.Validate(1.0000001m, null, null));
        Assert.Throws<InvalidOperationException>(() => FxPricing.Validate(1m, 0m, null));
        Assert.Throws<InvalidOperationException>(() => FxPricing.Calculate(0.00000001m, 99m, null, null));
        Assert.Throws<InvalidOperationException>(() => FxPricing.Calculate(1.000000001m, 1m, null, null));
    }

    [Fact]
    public void Default_schedule_is_three_hours_and_disabled_has_no_next_run()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now.AddHours(3), FxRateSchedule.Next(new FxRateScheduleDto(), now));
        Assert.Null(FxRateSchedule.Next(new FxRateScheduleDto() { Mode = "Disabled" }, now));
    }

    [Fact]
    public void Daily_schedule_uses_named_timezone_and_next_future_slot()
    {
        var schedule = new FxRateScheduleDto { Mode = "Daily", DailyTimes = ["18:00", "09:00"], TimeZoneId = "Africa/Lagos" };
        Assert.Equal(new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc),
            FxRateSchedule.Next(schedule, new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Spring_gap_is_skipped_and_fall_overlap_runs_only_once()
    {
        var spring = new FxRateScheduleDto { Mode = "Daily", DailyTimes = ["02:30"], TimeZoneId = "America/New_York" };
        Assert.Equal(new DateTime(2026, 3, 9, 6, 30, 0, DateTimeKind.Utc),
            FxRateSchedule.Next(spring, new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc)));
        var fall = new FxRateScheduleDto { Mode = "Daily", DailyTimes = ["01:30"], TimeZoneId = "America/New_York" };
        Assert.Equal(new DateTime(2026, 11, 2, 6, 30, 0, DateTimeKind.Utc),
            FxRateSchedule.Next(fall, new DateTime(2026, 11, 1, 5, 45, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Invalid_schedule_does_not_silently_fall_back_to_a_different_time()
    {
        Assert.Throws<InvalidOperationException>(() => FxRateSchedule.Validate(new() { IntervalMinutes = 0 }));
        Assert.Throws<InvalidOperationException>(() => FxRateSchedule.Validate(new() { Mode = "Daily", DailyTimes = ["09:00", "09:00"] }));
        Assert.Throws<InvalidOperationException>(() => FxRateSchedule.Validate(new() { Mode = "Daily", DailyTimes = ["25:00"] }));
        Assert.Throws<InvalidOperationException>(() => FxRateSchedule.Validate(new() { TimeZoneId = "Unknown/KorridorX" }));
    }
}
