namespace SplitServer.Requests;

public class VerifyAccountEmailRequest
{
    public required string Code { get; init; }
}
