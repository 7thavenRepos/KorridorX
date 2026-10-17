using System.ComponentModel.DataAnnotations;
using KorridorX.Dtos.Pagination;
using KorridorX.Infrastructure;

namespace KorridorX.Dtos.EmbeddedFinance;

/// <summary>Independent, bounded pages for the six read-only activity trace sections.</summary>
public sealed class EmbeddedFinanceTracePageQuery
{
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [Range(1, 1_000_000)] public int ProviderTransactionsPage { get; set; } = 1;
    [Range(1, 1_000_000)] public int ProviderRequestsPage { get; set; } = 1;
    [Range(1, 1_000_000)] public int ReservationsPage { get; set; } = 1;
    [Range(1, 1_000_000)] public int LedgerTransactionsPage { get; set; } = 1;
    [Range(1, 1_000_000)] public int JournalEntriesPage { get; set; } = 1;
    [Range(1, 1_000_000)] public int AuditTimelinePage { get; set; } = 1;

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

    public PageMeta ToMeta(string section, int total)
    {
        Validate();
        var page = section switch
        {
            "providerTransactions" => ProviderTransactionsPage,
            "providerRequests" => ProviderRequestsPage,
            "reservations" => ReservationsPage,
            "ledgerTransactions" => LedgerTransactionsPage,
            "journalEntries" => JournalEntriesPage,
            "auditTimeline" => AuditTimelinePage,
            _ => throw new ArgumentException("Unknown activity trace section.", nameof(section))
        };
        return new WebHistoryPageQuery { Page = page, PageSize = PageSize }.ToMeta(total);
    }
}

public sealed record EmbeddedFinanceAdminPagedActivityDetailDto(
    EmbeddedFinanceAdminActivityDetailDto Detail,
    IReadOnlyDictionary<string, PageMeta> Pages);
