namespace SplitServer.Responses;

public class InvitationResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required string GroupId { get; init; }
    public required string GroupName { get; init; }
    public required string? GuestId { get; init; }
    public required string? GuestName { get; init; }
}