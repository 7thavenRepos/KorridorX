using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Models.Notifications;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Notifications;

public sealed class SmtpNotificationDeliveryProvider : INotificationDeliveryProvider
{
    private readonly NotificationDeliveryOptions _options;
    private readonly ILogger<SmtpNotificationDeliveryProvider> _logger;
    private readonly string _frontendBaseUrl;

    public SmtpNotificationDeliveryProvider(
        IOptions<NotificationDeliveryOptions> options,
        ILogger<SmtpNotificationDeliveryProvider> logger,
        IOptions<SecurityOptions> securityOptions)
    {
        _options = options.Value;
        _logger = logger;
        _frontendBaseUrl = securityOptions.Value.Accounts.FrontendBaseUrl;
    }

    public async Task<NotificationDeliveryResult> SendAsync(
        NotificationMessage notification,
        CancellationToken ct = default)
    {
        if (!_options.Smtp.IsEnabled)
        {
            return new NotificationDeliveryResult(
                false,
                ErrorMessage: "SMTP notification delivery is disabled.");
        }

        if (!string.Equals(notification.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
        {
            return new NotificationDeliveryResult(
                false,
                ErrorMessage: $"Unsupported notification channel '{notification.Channel}'.");
        }

        try
        {
            var content = BrandedEmailTemplate.Render(notification.Subject, notification.Body, _frontendBaseUrl);
            using var message = new MailMessage
            {
                From = new MailAddress(_options.Smtp.FromAddress, _options.Smtp.FromName),
                Subject = notification.Subject,
                Body = content.PlainText,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                IsBodyHtml = false
            };
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                content.Html, Encoding.UTF8, MediaTypeNames.Text.Html));
            message.To.Add(notification.Recipient);

            using var smtp = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                smtp.Credentials = new NetworkCredential(
                    _options.Smtp.Username,
                    _options.Smtp.Password);
            }

            ct.ThrowIfCancellationRequested();
            await smtp.SendMailAsync(message);

            return new NotificationDeliveryResult(
                true,
                ProviderMessageId: notification.Id.ToString("N"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SMTP delivery failed for notification {NotificationId} to {Recipient}.",
                notification.Id,
                notification.Recipient);

            return new NotificationDeliveryResult(
                false,
                ErrorMessage: Truncate(ex.Message, 1000));
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
