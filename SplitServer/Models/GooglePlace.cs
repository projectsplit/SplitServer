namespace SplitServer.Models;

public record GooglePlace
{
    public required string Id { get; init; }
    
    public required string Name { get; init; }
    
    public required string Address { get; init; }
}