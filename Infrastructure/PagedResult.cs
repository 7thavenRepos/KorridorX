namespace KorridorX.Infrastructure;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public PageMeta Meta { get; set; } = new();
}