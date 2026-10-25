namespace SplitServer.Models;

/// <summary>
/// Something a person entered, and who entered it: a member id for a record in a group, a user id
/// for the others.
/// </summary>
public interface IHasCreator
{
    string CreatorId { get; }
}
