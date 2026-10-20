using System.Globalization;
using KorridorX.Dtos.Fx;
using KorridorX.Models.Fx;

namespace KorridorX.Services.Fx;

public static class FxRateSchedule
{
    public static FxRateScheduleDto Validate(FxRateScheduleDto? schedule)
    {
        if (schedule is null || schedule.Mode is not ("Disabled" or "Interval" or "Daily"))
            throw new InvalidOperationException("Choose a valid FX refresh schedule.");
        if (schedule.IntervalMinutes is < 1 or > 10080)
            throw new InvalidOperationException("Refresh interval must be from 1 to 10080 minutes.");
        var zone = (schedule.TimeZoneId ?? "").Trim();
        ResolveZone(zone);
        var times = (schedule.DailyTimes ?? []).Select(x => (x ?? "").Trim()).ToArray();
        if (schedule.Mode == "Daily" && (times.Length is < 1 or > 24 ||
            times.Distinct(StringComparer.Ordinal).Count() != times.Length ||
            times.Any(x => !TimeOnly.TryParseExact(x, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))))
            throw new InvalidOperationException("Choose 1 to 24 different daily times in HH:mm format.");
        return new FxRateScheduleDto { Mode = schedule.Mode, IntervalMinutes = schedule.IntervalMinutes,
            DailyTimes = schedule.Mode == "Daily" ? times.Order(StringComparer.Ordinal).ToArray() : [], TimeZoneId = zone };
    }

    public static DateTime? Next(FxProviderRateConfiguration config, DateTime afterUtc) =>
        Next(new FxRateScheduleDto { Mode = config.ScheduleMode, IntervalMinutes = config.IntervalMinutes,
            DailyTimes = config.DailyTimes.Split(',', StringSplitOptions.RemoveEmptyEntries), TimeZoneId = config.TimeZoneId }, afterUtc);

    public static DateTime? Next(FxRateScheduleDto schedule, DateTime afterUtc)
    {
        schedule = Validate(schedule);
        afterUtc = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        if (schedule.Mode == "Disabled") return null;
        if (schedule.Mode == "Interval") return afterUtc.AddMinutes(schedule.IntervalMinutes);
        var zone = ResolveZone(schedule.TimeZoneId);
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(afterUtc, zone).Date;
        for (var day = 0; day < 8; day++)
        {
            foreach (var time in schedule.DailyTimes)
            {
                var local = DateTime.SpecifyKind(localDate.AddDays(day).Add(TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan()), DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(local)) continue; // No invented wall-clock time across a spring-forward gap.
                var utc = zone.IsAmbiguousTime(local)
                    ? new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime
                    : TimeZoneInfo.ConvertTimeToUtc(local, zone); // Run only the first occurrence on a fall-back day.
                if (utc > afterUtc) return utc;
            }
        }
        throw new InvalidOperationException("No upcoming FX refresh time could be resolved.");
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100)
            throw new InvalidOperationException("A valid IANA timezone or UTC is required.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windows))
                return TimeZoneInfo.FindSystemTimeZoneById(windows);
            throw new InvalidOperationException("The FX schedule timezone is not supported.");
        }
        catch (InvalidTimeZoneException) { throw new InvalidOperationException("The FX schedule timezone is invalid."); }
    }
}
