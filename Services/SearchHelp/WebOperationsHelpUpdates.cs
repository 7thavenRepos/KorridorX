using System.Text.Json;

namespace KorridorX.Services.SearchHelp;

public static class WebOperationsHelpUpdates
{
    public static IReadOnlyList<HelpEntry> Apply(IReadOnlyList<HelpEntry> existing)
    {
        using var stream = typeof(HelpCatalog).Assembly.GetManifestResourceStream("KorridorX.Content.SearchHelp.web-operations-20261017.json")
            ?? throw new InvalidOperationException("The web operations help supplement is missing.");
        var additions = JsonSerializer.Deserialize<HelpEntry[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The web operations help supplement is empty.");
        // Preserve original articles and permissions. Enrich old entry points as well as adding searchable guides.
        foreach (var guide in existing.Where(x => !x.IsPage))
        {
            if (guide.Portal == "admin" && guide.PageRoute == "/admin/trading-wallets")
            {
                guide.Troubleshooting = [.. guide.Troubleshooting,
                    "For Instant trading only: choose Create missing house accounts or Treasury → House accounts if no eligible account exists. New balances are zero; funding and fresh FX pricing remain separate.",
                    "Bulk pair requests can partially succeed. Read created/skipped/failed counts. Reverse pairs start Paused for direction-specific review; the current status determines available controls."];
                guide.ReviewedOn = "2026-10-17";
            }
            if (guide.PageRoute.EndsWith("/support", StringComparison.Ordinal))
            {
                guide.Troubleshooting = [.. guide.Troubleshooting,
                    "For web validation, stale rows, pagination, pair status or house-account issues, record the workspace, business, reference, page/filter and redacted error. Never request tokens or passwords."];
                guide.ReviewedOn = "2026-10-17";
            }
        }
        return existing.Concat(additions).ToArray();
    }
}
