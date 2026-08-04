namespace SplitServer.Configuration;

public class PushNotificationsSettings : ISettings
{
    public string SectionName { get; init; } = "PushNotifications";

    /// <summary>VAPID public key, also handed to the browser so it can create a subscription.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>VAPID private key. Secret — supply via user-secrets or environment, never appsettings.json.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>VAPID subject: a "mailto:" or "https://" URI identifying this application to push services.</summary>
    public string Subject { get; set; } = string.Empty;
}
