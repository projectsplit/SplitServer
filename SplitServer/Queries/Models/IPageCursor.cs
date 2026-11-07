namespace SplitServer.Queries.Models;

/// <summary>A position in a list ordered newest first, given as the times of the item it is at.</summary>
public interface IPageCursor
{
    DateTime Created { get; }
    DateTime Occurred { get; }

    /// <summary>A link to one item, asking for the page around it rather than the page after it.</summary>
    bool IsJumpTo { get; }
}
