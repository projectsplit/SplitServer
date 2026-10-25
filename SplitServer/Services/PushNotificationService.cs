using System.Net;
using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Repositories;
using WebPush;

namespace SplitServer.Services;

public class PushNotificationService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PushNotificationsSettings _settings;
    private readonly IPushSubscriptionsRepository _pushSubscriptionsRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    // Reused across sends: this service is a singleton and a client per batch would churn sockets.
    // Vapid details are passed per call rather than set on the client, so it stays stateless and
    // safe to use from the concurrent background sends below.
    private readonly WebPushClient _client = new();

    // Null when no service account is configured. FirebaseApp is process-global and throws if the
    // same name is created twice, so it is built once here rather than per send.
    private readonly FirebaseMessaging? _firebaseMessaging;

    public PushNotificationService(
        IOptions<PushNotificationsSettings> settings,
        IPushSubscriptionsRepository pushSubscriptionsRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _settings = settings.Value;
        _pushSubscriptionsRepository = pushSubscriptionsRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _firebaseMessaging = CreateFirebaseMessaging(_settings.FirebaseServiceAccountJson);
    }

    public string PublicKey => _settings.PublicKey;

    /// <summary>
    /// False when neither transport is configured, which is the normal state for a local
    /// environment. Everything then no-ops instead of failing the request that triggered it.
    /// </summary>
    public bool IsEnabled => IsWebPushEnabled || _firebaseMessaging is not null;

    private bool IsWebPushEnabled =>
        !string.IsNullOrWhiteSpace(_settings.PublicKey) && !string.IsNullOrWhiteSpace(_settings.PrivateKey);

    /// <summary>
    /// A bad service account must not take the whole application down at startup — browser push and
    /// every unrelated endpoint still work without it — so a failure here disables Android push and
    /// says so, rather than throwing out of the constructor.
    /// </summary>
    private static FirebaseMessaging? CreateFirebaseMessaging(string serviceAccount)
    {
        if (string.IsNullOrWhiteSpace(serviceAccount))
        {
            return null;
        }

        try
        {
            var json = DecodeServiceAccount(serviceAccount);

            var app = FirebaseApp.GetInstance("split-push") ?? FirebaseApp.Create(
                new AppOptions { Credential = GoogleCredential.FromJson(json) },
                "split-push");

            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Firebase messaging could not be initialised; Android push is disabled");
            return null;
        }
    }

    /// <summary>
    /// Takes the credentials as configured and returns them as JSON. See
    /// <see cref="PushNotificationsSettings.FirebaseServiceAccountJson"/> for why both forms exist.
    /// </summary>
    private static string DecodeServiceAccount(string value)
    {
        var trimmed = value.Trim();

        return trimmed.StartsWith('{')
            ? trimmed
            : Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));
    }

    /// <summary>
    /// Sends a notification to every device of the given users without blocking the caller.
    /// Users who have not opted in are skipped. A push failure must never fail the action that
    /// triggered it, so this reports problems to the log and nowhere else.
    /// </summary>
    public void NotifyInBackground(IEnumerable<string> userIds, string title, string body, string? url = null)
    {
        var distinctUserIds = userIds.Distinct().ToList();

        if (!IsEnabled)
        {
            Log.Information("Push notification skipped: no push transport configured");
            return;
        }

        if (distinctUserIds.Count == 0)
        {
            return;
        }

        _ = Task.Run(() => Notify(distinctUserIds, title, body, url));
    }

    private async Task Notify(List<string> userIds, string title, string body, string? url)
    {
        try
        {
            var preferences = await _userPreferencesRepository.GetByIds(userIds, CancellationToken.None);

            var optedInUserIds = preferences
                .Where(x => x.PushNotificationsEnabled == true)
                .Select(x => x.Id)
                .ToList();

            if (optedInUserIds.Count == 0)
            {
                Log.Information(
                    "Push notification skipped: none of {UserCount} recipient(s) have push enabled",
                    userIds.Count);
                return;
            }

            var subscriptions = await _pushSubscriptionsRepository.GetAllByUserIds(optedInUserIds, CancellationToken.None);

            if (subscriptions.Count == 0)
            {
                Log.Information(
                    "Push notification skipped: {UserCount} opted-in recipient(s) have no registered device",
                    optedInUserIds.Count);
                return;
            }

            Log.Information(
                "Sending push notification to {SubscriptionCount} device(s) across {UserCount} user(s)",
                subscriptions.Count,
                optedInUserIds.Count);

            var payload = JsonSerializer.Serialize(new { title, body, url }, PayloadJsonOptions);
            var vapidDetails = new VapidDetails(_settings.Subject, _settings.PublicKey, _settings.PrivateKey);

            foreach (var subscription in subscriptions)
            {
                try
                {
                    if (subscription.Kind == PushDeviceKind.Fcm)
                    {
                        await SendViaFcm(subscription, title, body, url);
                    }
                    else
                    {
                        await SendViaWebPush(subscription, payload, vapidDetails);
                    }
                }
                catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    // The browser dropped this subscription, so it will never be deliverable again.
                    await _pushSubscriptionsRepository.Delete(subscription.Id, CancellationToken.None);
                }
                catch (FirebaseMessagingException ex) when (
                    ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    // FCM's equivalent of Gone: the app was uninstalled or the token was rotated, so
                    // this row addresses a device that no longer exists.
                    await _pushSubscriptionsRepository.Delete(subscription.Id, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send push notification to user {UserId}", subscription.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send push notifications");
        }
    }

    private async Task SendViaWebPush(
        Models.PushSubscription subscription,
        string payload,
        VapidDetails vapidDetails)
    {
        if (!IsWebPushEnabled)
        {
            return;
        }

        var webPushSubscription = new WebPush.PushSubscription(
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth);

        await _client.SendNotificationAsync(webPushSubscription, payload, vapidDetails);
    }

    /// <summary>
    /// Sent as a notification message rather than data-only so that Android draws it from the system
    /// tray while the app is backgrounded or killed — which is when notifications actually matter and
    /// exactly when no JavaScript of ours is running to draw one.
    /// </summary>
    private async Task SendViaFcm(Models.PushSubscription subscription, string title, string body, string? url)
    {
        if (_firebaseMessaging is null)
        {
            return;
        }

        var message = new Message
        {
            Token = subscription.Endpoint,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
            // Mirrors the Web Push payload so the tap handler reads the same field on both platforms.
            Data = url is null ? null : new Dictionary<string, string> { ["url"] = url },
            Android = new AndroidConfig { Priority = Priority.High }
        };

        await _firebaseMessaging.SendAsync(message);
    }
}
