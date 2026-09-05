namespace SplitServer.Configuration;

/// <summary>
/// Credentials for talking to Google Play about purchases. Play is the merchant here: it takes the
/// money, and this server's only job is to ask Play whether a purchase the client claims to have
/// made is real, and to record it if so.
/// </summary>
public class GooglePlaySettings : ISettings
{
    public string SectionName { get; init; } = "GooglePlay";

    /// <summary>
    /// Turns donations off entirely. When false nothing is verified and every client is told the
    /// feature does not exist, so an instance with no Play credentials never shows a button that
    /// could only fail.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The app's applicationId, which is what Play keys purchases by. Must match
    /// <c>capacitor.config.ts</c>'s appId and the Play Console listing, or every verification 404s.
    /// </summary>
    public string PackageName { get; set; } = "com.buqs";

    /// <summary>
    /// Service account key for an account granted "View financial data" and "Manage orders and
    /// subscriptions" on the Play Console app. Belongs in the gitignored
    /// appsettings.{Environment}.json alongside the other secrets, never in appsettings.json, which
    /// is committed.
    ///
    /// Accepts either the raw JSON or that JSON base64-encoded, on the same terms as
    /// <see cref="PushNotificationsSettings.FirebaseServiceAccountJson"/>: deployment passes it
    /// through an SSH script into a docker -e argument, which the quotes and newlines of raw JSON do
    /// not survive. JSON always begins with '{', so the two are told apart by the first character.
    /// </summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret for the Real-Time Developer Notifications endpoint, supplied as a
    /// <c>?token=</c> query parameter on the Pub/Sub push URL.
    /// </summary>
    /// <remarks>
    /// This only keeps strangers from making the endpoint do work. It is deliberately not what
    /// makes a notification trustworthy: the handler re-reads every purchase it is told about from
    /// the Play API before writing anything, so a forged notification with the right token still
    /// cannot invent a donation. Set it anyway — without it the endpoint is an open invitation to
    /// have this server hammer Google's API on demand.
    /// </remarks>
    public string NotificationSecret { get; set; } = string.Empty;
}
