namespace SplitServer.Requests;

public class ResetPasswordRequest
{
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}
