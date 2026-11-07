namespace SplitServer.Queries.Models;

public class NextExpensePageDetails : IPageCursor
{
    public required DateTime Created { get; init; }
    public required DateTime Occurred { get; init; }
    public bool IsJumpTo { get; init; }
}