namespace SplitServer.Models;

public record Location
{
    public required Coordinates Coordinates { get; init; }
    
    public required GooglePlace? Google { get; init; }
}