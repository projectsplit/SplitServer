namespace SplitServer.Models;

public record UserPreferences : EntityBase
{
    public required string? Currency { get; init; }
    public required string? TimeZone { get; init; }
}