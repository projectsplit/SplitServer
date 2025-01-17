namespace SplitServer.Models;

public class Guest
{
    public required string Id { get; init; }
    
    public required string Name { get; init; }
    
    public required DateTime Joined { get; init; }
}