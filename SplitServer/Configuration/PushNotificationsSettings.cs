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

    /// <summary>
    /// Firebase service account credentials, used to reach the Android app — the WebView it runs in
    /// has no Push API, so VAPID cannot address it and FCM is the only route. Secret: this is a
    /// private key granting send rights on the Firebase project. Supply via user-secrets or
    /// environment. Left empty, Android push is simply off and browser push carries on unaffected.
    ///
    /// Accepts either the raw JSON or that JSON base64-encoded. Deployment passes this through an
    /// SSH script into a docker -e argument, and the raw form is full of quotes and newlines that do
    /// not survive that intact, so base64 is the practical choice there. JSON always begins with '{',
    /// so the two are told apart by the first character rather than by a second setting.
    /// </summary>
    public string FirebaseServiceAccountJson { get; set; } = string.Empty;
}
