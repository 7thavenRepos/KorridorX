using System.Text.RegularExpressions;
namespace KorridorX.Services.SearchHelp;
public sealed class SearchQuery
{
    public string Text { get; }
    public string Folded { get; }
    public string[] Terms { get; }
    public Guid? ExactId { get; }
    public SearchQuery(string? text)
    {
        if ((text?.Length ?? 0) > 120) throw new ArgumentException("Search must not exceed 120 characters.");
        if ((text ?? "").Any(c => char.IsControl(c) && !char.IsWhiteSpace(c))) throw new ArgumentException("Search contains unsupported characters.");
        Text = Regex.Replace((text ?? "").Trim(), @"\s+", " ");
        Folded = Text.ToLowerInvariant();
        Terms = Folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (Terms.Length > 12) throw new ArgumentException("Use no more than 12 search words.");
        ExactId = Guid.TryParse(Text, out var id) ? id : null;
    }
    public bool CanSearch => Text.Length >= 2;
    public static string ExpandForGuides(string text)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["recipient"] = "beneficiary", ["recipients"] = "beneficiaries", ["payroll"] = "batches",
            ["crypto"] = "digital", ["cryptocurrency"] = "digital", ["2fa"] = "mfa",
            ["login"] = "sign", ["kyb"] = "verification", ["currencies"] = "currency",
            ["how"] = "", ["to"] = "", ["do"] = "", ["i"] = "", ["a"] = "", ["the"] = "" };
        return string.Join(' ', text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => aliases.GetValueOrDefault(t,t)).Where(t => t.Length > 0));
    }
    public int ScoreText(string title, string reference, string body)
    {
        if (!CanSearch) return 0;
        var expanded = ExpandForGuides($"{title} {reference} {body}");
        var wanted = ExpandForGuides(Folded).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (wanted.Length == 0 || wanted.Any(t => !expanded.Contains(t,StringComparison.Ordinal))) return 0;
        if (reference.Equals(Text,StringComparison.OrdinalIgnoreCase)) return 1000;
        if (title.Equals(Text,StringComparison.OrdinalIgnoreCase)) return 900;
        if (title.StartsWith(Text,StringComparison.OrdinalIgnoreCase)) return 750;
        if (title.Contains(Text,StringComparison.OrdinalIgnoreCase)) return 600;
        return 200 + wanted.Count(t => title.Contains(t,StringComparison.OrdinalIgnoreCase)) * 30;
    }
}
