namespace SplitServer.Models;

public record Coordinates
{
    public required double Latitude { get; init; }
    
    public required double Longitude { get; init; }
}