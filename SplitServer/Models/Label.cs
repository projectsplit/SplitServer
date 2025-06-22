namespace SplitServer.Models;

public record Label
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string Color { get; init; }

    public static readonly Label Empty = new() { Id = "", Text = "", Color = "" };
}