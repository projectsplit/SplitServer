
using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupResponse
{
    public required string Id { get; init; }
    public bool IsDeleted { get; init; }
    public required DateTime Created { get; init; }
    public required DateTime Updated { get; set; }
    public required string OwnerId { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
    public required List<GetGroupResponseMemberItem> Members { get; init; }
    public required List<Guest> Guests { get; init; }
    public required List<Label> Labels { get; init; }
}