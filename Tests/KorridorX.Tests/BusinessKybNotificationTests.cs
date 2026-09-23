using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;

namespace KorridorX.Tests;

public sealed class BusinessKybNotificationTests
{
    [Theory]
    [InlineData(KybStatus.UnderReview)]
    [InlineData(KybStatus.Approved)]
    [InlineData(KybStatus.Rejected)]
    [InlineData(KybStatus.Pending)]
    public void Email_encodes_business_name_and_links_to_authenticated_portal(KybStatus status)
    {
        var result = BusinessKybNotificationService.CreateContent(status,
            "<script>alert('business')</script>", "https://staging.example.test/portal/");
        Assert.DoesNotContain("<script>", result.Body);
        Assert.Contains("&lt;script&gt;", result.Body);
        Assert.Contains("href=\"https://staging.example.test/portal/business/verification\"", result.Body);
        Assert.DoesNotContain("<script>", result.Subject);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test?redirect=other")]
    [InlineData("https://example.test/#other")]
    [InlineData("not-a-url")]
    public void Email_rejects_unsafe_portal_base(string url) =>
        Assert.Throws<InvalidOperationException>(() =>
            BusinessKybNotificationService.CreateContent(KybStatus.Approved, "Test company", url));
}
