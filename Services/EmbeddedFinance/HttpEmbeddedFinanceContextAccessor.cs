namespace KorridorX.Services.EmbeddedFinance;

public sealed class HttpEmbeddedFinanceContextAccessor : IEmbeddedFinanceContextAccessor
{
    public const string HttpContextItemKey = "KorridorX.EmbeddedFinancePrincipal";
    private readonly IHttpContextAccessor _httpContextAccessor;
    public HttpEmbeddedFinanceContextAccessor(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;
    public EmbeddedFinancePrincipal GetRequiredPrincipal() => _httpContextAccessor.HttpContext?.Items[HttpContextItemKey] as EmbeddedFinancePrincipal ?? throw new UnauthorizedAccessException("Embedded Finance API authentication is required.");
}
