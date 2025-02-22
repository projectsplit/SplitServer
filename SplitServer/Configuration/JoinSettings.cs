namespace SplitServer.Configuration;

public class JoinSettings : ISettings
{
    public required string SectionName { get; init; } = "Join";
    public required int TokenExpirationInSeconds { get; init; }
    public required int MaxTokenUses { get; init; }
    public required int TokenLength { get; init; }
}