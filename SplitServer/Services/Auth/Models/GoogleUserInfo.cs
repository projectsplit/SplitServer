namespace SplitServer.Services.Auth.Models;

public class GoogleUserInfo
{
    public required string Id { get; init; }
    public required string Email { get; init; }

    /// <summary>
    /// Whether Google itself has proven the user owns this address. Google does not guarantee it,
    /// so an address must never be treated as owned, nor used to find an account, without it.
    /// </summary>
    public required bool EmailVerified { get; init; }

    public required string? Name { get; init; }
}