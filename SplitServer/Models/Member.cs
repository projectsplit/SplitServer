namespace SplitServer.Models;

public record Member
{
    public required string Id { get; init; }
    
    public required string UserId { get; init; }
    
    // public required List<string> RoleIds { get; init; }
    
    public DateTime Joined { get; init; }
}