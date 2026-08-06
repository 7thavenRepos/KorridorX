namespace KorridorX.Services.BusinessContext;

public sealed class HttpBusinessContextAccessor : IBusinessContextAccessor
{
    public const string HeaderName = "X-Business-Profile-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpBusinessContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetSelectedBusinessProfileId()
    {
        var value = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Guid.TryParse(value, out var businessProfileId))
        {
            throw new InvalidOperationException(
                $"The {HeaderName} header must contain a valid business profile ID.");
        }

        return businessProfileId;
    }
}
