namespace SplitServer.Dto;

public class CreateInvitationRequest
{
    public required string ToId { get; set; }
    public required string GroupId { get; init; }
    public required string? GuestId { get; init; }
}