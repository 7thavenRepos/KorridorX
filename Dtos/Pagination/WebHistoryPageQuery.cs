using System.ComponentModel.DataAnnotations;
using KorridorX.Infrastructure;

namespace KorridorX.Dtos.Pagination;

/// <summary>Bounded read-only history paging. The legacy list endpoints remain unchanged.</summary>
public sealed class WebHistoryPageQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public PageMeta ToMeta(int totalItems)
    {
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
        ArgumentOutOfRangeException.ThrowIfNegative(totalItems);
        var lastPage = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        return new PageMeta
        {
            Page = Math.Min(Page, lastPage),
            PageSize = PageSize,
            TotalItems = totalItems
        };
    }
}
