namespace KorridorX.Services.References;

public class ReferenceGenerator : IReferenceGenerator
{
    public string GenerateTransferReference()
    {
        return Generate("KXTR");
    }

    public string GenerateCollectionReference()
    {
        return Generate("KXCOL");
    }

    public string GeneratePayoutReference()
    {
        return Generate("KXPO");
    }

    private static string Generate(string prefix)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(100000, 999999);

        return $"{prefix}-{date}-{random}";
    }
}