namespace SplitServer.Models;

public record EntityBase
{
    public required string Id { get; init; }
    
    public required bool IsDeleted { get; init; }
    
    public required DateTime Created { get; init; }
    
    public required DateTime Updated { get; init; }
}