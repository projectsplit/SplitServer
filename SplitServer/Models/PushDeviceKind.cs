namespace SplitServer.Models;

/// <summary>
/// Which push transport a stored device is reachable over. WebPush is 0 so that rows written before
/// this existed — every row at the time it was added — keep deserializing as what they actually are
/// rather than silently becoming FCM devices and failing every send.
/// </summary>
public enum PushDeviceKind
{
    WebPush = 0,
    Fcm = 1
}
