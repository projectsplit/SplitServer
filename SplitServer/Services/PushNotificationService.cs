using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
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
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        IOptions<PushNotificationsSettings> settings,
        IPushSubscriptionsRepository pushSubscriptionsRepository,
        IUserPreferencesRepository userPreferencesRepository,
        ILogger<PushNotificationService> logger)
    {
        _settings = settings.Value;
        _pushSubscriptionsRepository = pushSubscriptionsRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _logger = logger;
    }

    public string PublicKey => _settings.PublicKey;

    public bool IsEnabled => !string.IsNullOrEmpty(_settings.PublicKey) && !string.IsNullOrEmpty(_settings.PrivateKey);

    /// <summary>
    /// Sends a push notification to every device of the given users without blocking the caller.
    /// Users that have push notifications disabled (or never enabled them) are skipped.
    /// </summary>
    public void NotifyInBackground(IEnumerable<string> userIds, string title, string body, string? url = null)
    {
        var distinctUserIds = userIds.Distinct().ToList();

        if (!IsEnabled || distinctUserIds.Count == 0)
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

            var optedInUserIds = userIds
                .Where(id => preferences.FirstOrDefault(p => p.Id == id)?.PushNotificationsEnabled == true)
                .ToList();

            if (optedInUserIds.Count == 0)
            {
                return;
            }

            var subscriptions = await _pushSubscriptionsRepository.GetAllByUserIds(optedInUserIds, CancellationToken.None);

            if (subscriptions.Count == 0)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(new { title, body, url }, PayloadJsonOptions);
            var vapidDetails = new VapidDetails(_settings.Subject, _settings.PublicKey, _settings.PrivateKey);

            using var client = new WebPushClient();

            foreach (var subscription in subscriptions)
            {
                try
                {
                    var webPushSubscription = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);

                    await client.SendNotificationAsync(webPushSubscription, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    await _pushSubscriptionsRepository.Delete(subscription.Id, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send push notification to user {UserId}", subscription.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notifications");
        }
    }
}
