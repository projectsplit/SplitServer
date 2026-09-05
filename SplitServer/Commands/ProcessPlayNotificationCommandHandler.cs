using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Repositories;
using SplitServer.Services.Donations;

namespace SplitServer.Commands;

/// <summary>
/// Play telling us something changed. Renewals, cancellations, expiries and refunds all arrive here
/// months after the purchase that started them, and this is the only thing that keeps a monthly
/// gift's state true over time — the app is not running when a renewal is billed.
///
/// The body is never acted on directly. Every branch below reduces the notification to a purchase
/// token, hands it to <see cref="DonationRecorder"/>, and that re-reads the purchase from Google.
/// A forged notification therefore cannot invent, alter or cancel a donation; the worst it can do
/// is make this server ask Google about a purchase, which is what the shared secret on the URL is
/// there to stop.
///
/// Failing and refusing are different answers. A <c>Result</c> failure means the body will never be
/// acceptable — a bad secret, unparseable JSON, the wrong app — and becomes a 400 so Pub/Sub stops
/// redelivering. A write that did not land throws instead, becoming a 500 that Pub/Sub does retry,
/// because that one really can succeed on the next attempt.
/// </summary>
public class ProcessPlayNotificationCommandHandler : IRequestHandler<ProcessPlayNotificationCommand, Result>
{
    private static readonly JsonSerializerOptions NotificationJson = new(JsonSerializerDefaults.Web);

    private readonly GooglePlayBillingService _play;
    private readonly DonationRecorder _recorder;
    private readonly IDonationsRepository _donationsRepository;

    public ProcessPlayNotificationCommandHandler(
        GooglePlayBillingService play,
        DonationRecorder recorder,
        IDonationsRepository donationsRepository)
    {
        _play = play;
        _recorder = recorder;
        _donationsRepository = donationsRepository;
    }

    public async Task<Result> Handle(ProcessPlayNotificationCommand command, CancellationToken ct)
    {
        if (!_play.IsNotificationSecretValid(command.Secret))
        {
            return Result.Failure("Invalid notification secret");
        }

        if (!_play.IsConfigured)
        {
            return Result.Failure("Donations are not available");
        }

        var notificationResult = Decode(command.Payload);

        if (notificationResult.IsFailure)
        {
            return notificationResult;
        }

        var notification = notificationResult.Value;

        // A topic can be shared between apps, and a misconfigured console can point someone else's
        // notifications at this endpoint. Acting on those would have us query Google for purchases
        // that are not ours on every message.
        if (notification.PackageName != _play.PackageName)
        {
            return Result.Failure($"Notification was for {notification.PackageName}, not {_play.PackageName}");
        }

        if (notification.TestNotification is not null)
        {
            Log.Information("Received the Google Play test notification");

            return Result.Success();
        }

        if (notification.SubscriptionNotification is { PurchaseToken: { } subscriptionToken })
        {
            return await _recorder.RecordSubscriptionPurchase(subscriptionToken, expectedUserId: null, ct);
        }

        if (notification.OneTimeProductNotification is { PurchaseToken: { } productToken, Sku: { } sku })
        {
            return await _recorder.RecordProductPurchase(sku, productToken, expectedUserId: null, ct);
        }

        if (notification.VoidedPurchaseNotification is { PurchaseToken: { } voidedToken })
        {
            return await HandleVoided(voidedToken, ct);
        }

        // A notification shape this build does not know about. Succeeding is what stops Pub/Sub
        // redelivering something nothing here will ever act on.
        return Result.Success();
    }

    /// <summary>
    /// A refund or chargeback. Handled by re-reading the purchase rather than by believing the
    /// notification: a refunded one-off comes back from Play in the cancelled state and a revoked
    /// subscription comes back inactive, so the ordinary recording path writes the right thing
    /// without a separate "mark it refunded" route that a forged message could drive.
    /// </summary>
    private async Task<Result> HandleVoided(string purchaseToken, CancellationToken ct)
    {
        // The notification does not name the product for a one-off, and the Play API cannot be
        // queried without one. Our own row is where that was written down at purchase time.
        var donationMaybe = await _donationsRepository.GetByPurchaseToken(purchaseToken, ct);

        if (donationMaybe.HasNoValue)
        {
            Log.Warning("Google Play voided a purchase this server has no record of");

            return Result.Success();
        }

        var donation = donationMaybe.Value;

        return donation.SubscriptionId is not null
            ? await _recorder.RecordSubscriptionPurchase(purchaseToken, expectedUserId: null, ct)
            : await _recorder.RecordProductPurchase(donation.ProductId, purchaseToken, expectedUserId: null, ct);
    }

    private static Result<PlayDeveloperNotification> Decode(string payload)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<PubSubPushEnvelope>(payload, NotificationJson);

            if (envelope?.Message?.Data is not { } data)
            {
                return Result.Failure<PlayDeveloperNotification>("Push envelope carried no message data");
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(data));

            var notification = JsonSerializer.Deserialize<PlayDeveloperNotification>(json, NotificationJson);

            return notification is null
                ? Result.Failure<PlayDeveloperNotification>("Notification was empty")
                : notification;
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return Result.Failure<PlayDeveloperNotification>($"Could not read the notification: {ex.Message}");
        }
    }
}
