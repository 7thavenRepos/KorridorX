using System.Text.Encodings.Web;
using KorridorX.Configuration;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Services.Notifications;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Compliance;

// Enqueues only. The caller persists the notification with its KYB state change.
public sealed class BusinessKybNotificationService(
    INotificationQueueService queue,
    IOptions<SecurityOptions> security)
{
    public const string EntityType = "BusinessKybApplication";

    public Task QueueStatusChangeAsync(
        BusinessKybApplication application,
        KybStatus previousStatus,
        CancellationToken ct = default)
    {
        if (application.Status == previousStatus)
            return Task.CompletedTask;

        var content = CreateContent(application.Status, application.BusinessProfile.BusinessName,
            security.Value.Accounts.FrontendBaseUrl);

        // KYB is private business information. Do not use QueueBusinessAsync,
        // which also sends to every active business member.
        return queue.QueueUserAsync(application.BusinessProfile.OwnerUserId,
            content.Subject, content.Body, EntityType, application.Id, ct);
    }

    public static (string Subject, string Body) CreateContent(
        KybStatus status, string businessName, string frontendBaseUrl)
    {
        var (subject, message) = status switch
        {
            KybStatus.UnderReview => (
                "Your business verification is under review",
                "We received your business verification submission. It is now under review. We will email you when the review status changes."),
            KybStatus.Approved => (
                "Your business verification is approved",
                "Your business verification has been approved. Sign in to see the services available to your business."),
            KybStatus.Rejected => (
                "Your business verification needs attention",
                "Your business verification was not approved. Sign in to review the feedback and any requested corrections before submitting again."),
            KybStatus.Pending => (
                "Your business verification status has changed",
                "Your business verification is now pending. Sign in to review the application and any outstanding steps."),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported KYB notification status.")
        };

        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("A valid account frontend URL is required for KYB notifications.");

        var encode = HtmlEncoder.Default;
        var url = frontendBaseUrl.TrimEnd('/') + "/business/verification";
        return (subject,
            $"<p>{encode.Encode(businessName)}</p>" +
            $"<p>{encode.Encode(message)}</p>" +
            $"<p><a href=\"{encode.Encode(url)}\">View business verification</a></p>" +
            "<p>For your privacy, verification documents and review details are available only after signing in.</p>");
    }
}
