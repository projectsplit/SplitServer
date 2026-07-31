namespace SplitServer.Models;

public record EmailVerificationCode : EntityBase
{
    public required string UserId { get; init; }
    public required string CodeHash { get; init; }
    public required EmailVerificationCodePurpose Purpose { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required DateTime? ConsumedAt { get; init; }
}
