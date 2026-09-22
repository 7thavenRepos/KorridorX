namespace KorridorX.Services.Compliance;

/// <summary>Preserves a KYB calendar day while matching the existing UTC database columns.</summary>
public static class BusinessKybCalendarDate
{
    public static DateTime Normalize(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
