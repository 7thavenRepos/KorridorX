using KorridorX.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Extensions;

public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> PaginateAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            }
        };
    }
}