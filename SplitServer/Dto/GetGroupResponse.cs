
using SplitServer.Models;

namespace SplitServer.Dto;

public class GetGroupResponse
{    
    public required string Id { get; init; }
    
    public bool IsDeleted { get; init; }
    
    public DateTime Created { get; init; }
    
    public DateTime Updated { get; set; }

    public required string OwnerId { get; init; }
    
    public required string Name { get; init; }
    
    public required string Currency { get; init; }
    
    public required List<GetGroupResponseMember> Members { get; init; }
    
    public required List<Guest> Guests { get; init; }
    
    public required List<Label> Labels { get; init; }
}