using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;
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

    public PushNotificationService(
        IOptions<PushNotificationsSettings> settings,
        IPushSubscriptionsRepository pushSubscriptionsRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _settings = settings.Value;
        _pushSubscriptionsRepository = pushSubscriptionsRepository;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public string PublicKey => _settings.PublicKey;

    /// <summary>
    /// False when no VAPID key pair is configured, which is the normal state for a local
    /// environment. Everything then no-ops instead of failing the request that triggered it.
    /// </summary>
    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(_settings.PublicKey) && !string.IsNullOrWhiteSpace(_settings.PrivateKey);

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
            Log.Information("Push notification skipped: no VAPID keys configured");
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
                    var webPushSubscription = new WebPush.PushSubscription(
                        subscription.Endpoint,
                        subscription.P256dh,
                        subscription.Auth);

                    await _client.SendNotificationAsync(webPushSubscription, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    // The browser dropped this subscription, so it will never be deliverable again.
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
}
