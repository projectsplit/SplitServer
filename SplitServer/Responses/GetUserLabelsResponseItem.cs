using SplitServer.Models;
namespace SplitServer.Responses;

public class GetUserLabelsResponse
{
    public required string UserId { get; init; }
    public required string Text { get; init; }
    public required string Color { get; init; }
}