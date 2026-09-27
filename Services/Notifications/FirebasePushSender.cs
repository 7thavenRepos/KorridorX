using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using KorridorX.Configuration;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Notifications;

public interface IFirebasePushSender
{
    Task<string> SendAsync(
        Message message,
        CancellationToken ct = default);
}

public sealed class FirebasePushSender : IFirebasePushSender, IDisposable
{
    private readonly NotificationDeliveryOptions _options;
    private readonly object _gate = new();
    private FirebaseApp? _app;

    public FirebasePushSender(
        IOptions<NotificationDeliveryOptions> options)
    {
        _options = options.Value;
    }

    public Task<string> SendAsync(
        Message message,
        CancellationToken ct = default)
    {
        var messaging = FirebaseMessaging.GetMessaging(GetOrCreateApp());
        return messaging.SendAsync(message, ct);
    }

    private FirebaseApp GetOrCreateApp()
    {
        if (_app is not null)
            return _app;

        lock (_gate)
        {
            if (_app is not null)
                return _app;

            var projectId = _options.Firebase.ProjectId.Trim();

            if (projectId.Length == 0)
                throw new InvalidOperationException(
                    "Firebase push delivery requires a project ID.");

            _app = FirebaseApp.Create(
                new AppOptions
                {
                    Credential = GoogleCredential.GetApplicationDefault(),
                    ProjectId = projectId
                },
                $"KorridorXNotifications-{Guid.NewGuid():N}");

            return _app;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _app?.Delete();
            _app = null;
        }
    }
}
