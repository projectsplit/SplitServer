using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupsResponseItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; init; }
    public required string OwnerId { get; init; }
    public required string Currency { get; init; }
    public required bool IsArchived { get; init; }
    public required List<Member> Members { get; init; }
    public required List<Guest> Guests { get; init; }
    public required List<Label> Labels { get; init; }
}