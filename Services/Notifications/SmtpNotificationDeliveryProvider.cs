using System.Net;
using System.Net.Mail;
using KorridorX.Configuration;
using KorridorX.Models.Notifications;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Notifications;

public sealed class SmtpNotificationDeliveryProvider : INotificationDeliveryProvider
{
    private readonly NotificationDeliveryOptions _options;
    private readonly ILogger<SmtpNotificationDeliveryProvider> _logger;

    public SmtpNotificationDeliveryProvider(
        IOptions<NotificationDeliveryOptions> options,
        ILogger<SmtpNotificationDeliveryProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
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
            using var message = new MailMessage
            {
                From = new MailAddress(_options.Smtp.FromAddress, _options.Smtp.FromName),
                Subject = notification.Subject,
                Body = notification.Body,
                IsBodyHtml = true
            };
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
