namespace SplitServer.Configuration;

public class PushNotificationsSettings : ISettings
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string SectionName { get; init; } = "PushNotifications";
}
