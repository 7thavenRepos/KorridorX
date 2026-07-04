namespace KorridorX.Infrastructure;

public class PageMeta
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}