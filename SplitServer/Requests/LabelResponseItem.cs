namespace SplitServer.Requests;

public class LabelResponseItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string Color { get; init; }
    public required long Count { get; init; }
}