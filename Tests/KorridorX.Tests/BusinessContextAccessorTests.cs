using KorridorX.Services.BusinessContext;
using Microsoft.AspNetCore.Http;

namespace KorridorX.Tests;

public class BusinessContextAccessorTests
{
    [Fact]
    public void GetSelectedBusinessProfileId_ReturnsNullWhenHeaderIsMissing()
    {
        var accessor = CreateAccessor();

        Assert.Null(accessor.GetSelectedBusinessProfileId());
    }

    [Fact]
    public void GetSelectedBusinessProfileId_ReturnsHeaderValue()
    {
        var expected = Guid.NewGuid();
        var accessor = CreateAccessor(expected.ToString());

        Assert.Equal(expected, accessor.GetSelectedBusinessProfileId());
    }

    [Fact]
    public void GetSelectedBusinessProfileId_RejectsInvalidHeader()
    {
        var accessor = CreateAccessor("not-a-guid");

        Assert.Throws<InvalidOperationException>(() => accessor.GetSelectedBusinessProfileId());
    }

    private static HttpBusinessContextAccessor CreateAccessor(string? header = null)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
            context.Request.Headers[HttpBusinessContextAccessor.HeaderName] = header;

        return new HttpBusinessContextAccessor(new HttpContextAccessor
        {
            HttpContext = context
        });
    }
}
