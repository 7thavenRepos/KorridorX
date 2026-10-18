using System.Text.Json;
using KorridorX.Dtos.SearchHelp;

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
            if (guide.Portal == "admin" && guide.PageRoute == "/admin/master-data")
            {
                guide.Keywords = [.. guide.Keywords, "KX-010", "multiple assets", "unmapped assets", "country asset mapping", "bulk country assets"];
                guide.Steps = [.. guide.Steps,
                    new GuideStepDto("Add several assets to one country", "Open Country & asset mappings. Choose a country; the Assets not yet mapped selector excludes every existing mapping, including disabled rows and later table pages. Select one or more assets (Ctrl/Command-click on desktop)."),
                    new GuideStepDto("Review shared capabilities and save once", "Choose only capabilities supported by the country and every selected new asset. Enter an audit reason and select Add asset mappings. All selected assets share these choices. An invalid new mapping rejects the new batch; none of its new rows is saved. Use separate batches for different capabilities."),
                    new GuideStepDto("Check created and existing counts", "The list and selector refresh immediately after success; the country stays selected. Retries and overlapping requests leave existing mappings unchanged. New rows are non-default; use Configure to deliberately change a default or edit an existing row.")];
                guide.Troubleshooting = [.. guide.Troubleshooting,
                    "An empty selector can mean no country was selected or all configured assets are already mapped. A failed load disables creation until Refresh succeeds. Disabled mappings are still existing; edit them with Configure.",
                    "Changing country clears asset selections; refreshing removes assets mapped by another administrator. After a lost response, refresh before retrying. A capability error names the incompatible asset; correct the batch, not the database."];
                guide.ReviewedOn = "2026-10-17";
            }
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
        foreach (var guide in existing.Concat(additions).Where(x => !x.IsPage))
        {
            if (guide.PageRoute.EndsWith("/support", StringComparison.Ordinal) || guide.Slug == "kx009-web-operations-support-triage")
            {
                guide.Keywords = [.. guide.Keywords, "KX-011", "deposit channel", "pending bank account", "missing deposit", "KX-012", "Interac receiving email", "sending email verification", "unmatched Interac"];
                guide.Troubleshooting = [.. guide.Troubleshooting,
                    "Payment channels: collect the workspace, wallet currency, channel display name, collection/account reference, amount/time and redacted error. Never request PAN, CVC, passwords, tokens or provider credentials.",
                    "No methods may be correct for the country/audience/provider environment. Ask SuperAdmin to review Payment channels. NGN virtual accounts support verified businesses and individuals; non-NGN virtual accounts currently support individuals only.",
                    "Account setup and checkout redirects do not credit balances. Unknown submission outcomes require status/reconciliation, not another payment. Provider-development GBP/EUR setup is on hold and USD deposits do not settle.",
                    "Persistent CAD Interac: collect the wallet/workspace, receiving email, the actual sending email shown by the bank, bank/provider reference, amount and time. Never request a verification code. Verified registration email is the personal default; a business needs its own verified sending email. One sending email cannot identify different personal or business wallets on the same receiving route.",
                    "For code expiry, resend cooldown or ownership conflict, use Wallet / Add money sender management and the KX-012 funding guide. Removing a sender stops future matching and does not release its historical ownership to another wallet.",
                    "Missing unsolicited deposits: SuperAdmin reviews Interac receiving → retained deposits, records findings in audit notes, and checks provider-confirmed net amount and sender/receiving email. The review screen cannot credit, reassign or replay funds. Money Requests and dedicated bank deposits keep their existing status and reconciliation workflows."];
                guide.ReviewedOn = "2026-10-18";
            }
        }
        return existing.Concat(additions).ToArray();
    }
}
