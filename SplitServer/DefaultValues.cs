using SplitServer.Models;

namespace SplitServer;

public static class DefaultValues
{
    public const string Currency = "EUR";
    public const string TimeZone = "Europe/Athens";
    public static readonly Coordinates Coordinates = new() { Latitude = 37.96667, Longitude = 23.71667 };
}