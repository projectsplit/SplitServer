namespace SplitServer.Repositories.Implementations.Models;

public record LabelCount
{
    public required string Label { get; init; }
    public required int Count { get; init; }
}