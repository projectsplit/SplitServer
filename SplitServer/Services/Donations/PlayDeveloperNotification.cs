namespace SplitServer.Services.Donations;

/// <summary>
/// The envelope Google Cloud Pub/Sub posts when Play has something to say. Play publishes to a
/// topic, Pub/Sub pushes it here, and the notification itself is base64 inside
/// <see cref="PubSubMessage.Data"/>.
/// </summary>
public class PubSubPushEnvelope
{
    public PubSubMessage? Message { get; init; }
}

public class PubSubMessage
{
    /// <summary>Base64 of the <see cref="PlayDeveloperNotification"/> JSON.</summary>
    public string? Data { get; init; }

    public string? MessageId { get; init; }
}

/// <summary>
/// Play's Real-Time Developer Notification. Exactly one of the notification blocks is populated.
///
/// Treated purely as a hint about which purchase to go and look at. Nothing on it is written to the
/// ledger — not the amount, not the state, not who it belongs to — because a notification is an
/// unauthenticated HTTP body and the Play API is not. Everything downstream re-reads the purchase
/// from Google and writes that.
/// </summary>
public class PlayDeveloperNotification
{
    public string? PackageName { get; init; }

    public PlaySubscriptionNotification? SubscriptionNotification { get; init; }

    public PlayOneTimeProductNotification? OneTimeProductNotification { get; init; }

    public PlayVoidedPurchaseNotification? VoidedPurchaseNotification { get; init; }

    /// <summary>Present only for the "send test notification" button in the Play Console.</summary>
    public PlayTestNotification? TestNotification { get; init; }
}

public class PlaySubscriptionNotification
{
    public int NotificationType { get; init; }

    public string? PurchaseToken { get; init; }

    /// <summary>The subscription product id. Play calls it this for historical reasons.</summary>
    public string? SubscriptionId { get; init; }
}

public class PlayOneTimeProductNotification
{
    public int NotificationType { get; init; }

    public string? PurchaseToken { get; init; }

    /// <summary>The in-app product id. Play calls it this for historical reasons.</summary>
    public string? Sku { get; init; }
}

public class PlayVoidedPurchaseNotification
{
    public string? PurchaseToken { get; init; }

    public string? OrderId { get; init; }

    /// <summary>1 for a subscription, 2 for a one-off.</summary>
    public int ProductType { get; init; }
}

public class PlayTestNotification
{
    public string? Version { get; init; }
}
