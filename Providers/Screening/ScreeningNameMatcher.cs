using System.Globalization;
using System.Text;

namespace KorridorX.Providers.Screening;

public static class ScreeningNameMatcher
{
    public static decimal CalculateScore(string candidate, string watchlistName)
    {
        var left = Normalize(candidate);
        var right = Normalize(watchlistName);

        if (left.Length == 0 || right.Length == 0)
            return 0m;
        if (string.Equals(left, right, StringComparison.Ordinal))
            return 100m;

        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var union = leftTokens.Union(rightTokens).Count();
        var intersection = leftTokens.Intersect(rightTokens).Count();
        var tokenScore = union == 0 ? 0m : 100m * intersection / union;

        var distance = LevenshteinDistance(left, right);
        var maxLength = Math.Max(left.Length, right.Length);
        var editScore = maxLength == 0 ? 0m : 100m * (1m - (decimal)distance / maxLength);

        return Math.Round(Math.Max(tokenScore, editScore), 2, MidpointRounding.AwayFromZero);
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
