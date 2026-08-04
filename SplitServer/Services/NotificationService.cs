using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services;

/// <summary>
/// The single way to tell users something happened. Records the event in each recipient's in-app
/// feed and delivers it as a push, so the two can never drift apart.
/// </summary>
public class NotificationService
{
    private readonly INotificationsRepository _notificationsRepository;
    private readonly PushNotificationService _pushNotificationService;

    public NotificationService(
        INotificationsRepository notificationsRepository,
        PushNotificationService pushNotificationService)
    {
        _notificationsRepository = notificationsRepository;
        _pushNotificationService = pushNotificationService;
    }

    /// <summary>
    /// The feed entry is written for every recipient regardless of their push preference: opting
    /// out of push means not wanting to be interrupted, not wanting no record. Push is then
    /// attempted separately and only reaches those who opted in.
    /// </summary>
    public async Task Notify(
        IEnumerable<string> userIds,
        string title,
        string body,
        string? url,
        CancellationToken ct)
    {
        var distinctUserIds = userIds.Distinct().ToList();

        if (distinctUserIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var notifications = distinctUserIds
            .Select(x => new Notification
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                UserId = x,
                Title = title,
                Body = body,
                Url = url,
            })
            .ToList();

        var writeResult = await _notificationsRepository.InsertMany(notifications, ct);

        // Never fail the action that triggered this: the expense or transfer is already written,
        // and losing its notification is not worth rejecting it over.
        if (writeResult.IsFailure)
        {
            Log.Warning("Failed to write notifications: {Error}", writeResult.Error);
        }

        _pushNotificationService.NotifyInBackground(distinctUserIds, title, body, url);
    }
}
