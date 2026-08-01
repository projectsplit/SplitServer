using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;
using SplitServer.Services.Email;

namespace SplitServer.Commands;

public class VerifyAccountEmailCommandHandler : IRequestHandler<VerifyAccountEmailCommand, Result>
{
    private const string InvalidOrExpiredError = "Invalid or expired code";
    // Kept in sync with ALREADY_CLAIMED_MARKER in the webapp's EditEmail component, which uses it
    // to send the user back to the email step instead of leaving them retrying the code.
    private const string AlreadyClaimedError =
        "This email is already associated with another account. Please use a different email address.";

    private readonly IUsersRepository _usersRepository;
    private readonly IEmailVerificationCodesRepository _codesRepository;
    private readonly EmailTokenService _tokenService;
    private readonly LockService _lockService;

    public VerifyAccountEmailCommandHandler(
        IUsersRepository usersRepository,
        IEmailVerificationCodesRepository codesRepository,
        EmailTokenService tokenService,
        LockService lockService)
    {
        _usersRepository = usersRepository;
        _codesRepository = codesRepository;
        _tokenService = tokenService;
        _lockService = lockService;
    }

    public async Task<Result> Handle(VerifyAccountEmailCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Code))
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var codeHash = _tokenService.Hash(command.Code);

        var codeMaybe = await _codesRepository.GetActiveByCodeHash(
            codeHash,
            EmailVerificationCodePurpose.VerifyEmail,
            ct);

        if (codeMaybe.HasNoValue)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var code = codeMaybe.Value;

        if (code.UserId != command.UserId)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var now = DateTime.UtcNow;

        if (code.ExpiresAt <= now || code.ConsumedAt is not null)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var userMaybe = await _usersRepository.GetById(code.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var user = userMaybe.Value;

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        // Sign-up lets several accounts hold the same unverified email, so this is the point where
        // exactly one of them takes ownership. The lock keeps two concurrent claims from both winning.
        using var _ = _lockService.AcquireLock($"verify-email:{user.Email.ToLowerInvariant()}");

        var existingOwnerMaybe = await _usersRepository.GetVerifiedByEmail(user.Email, ct);

        if (existingOwnerMaybe.HasValue && existingOwnerMaybe.Value.Id != user.Id)
        {
            return Result.Failure(AlreadyClaimedError);
        }

        var updateUserResult = await _usersRepository.Update(
            user with
            {
                EmailVerified = true,
                Updated = now,
            },
            ct);

        if (updateUserResult.IsFailure)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var consumeResult = await _codesRepository.Update(
            code with
            {
                ConsumedAt = now,
                Updated = now,
            },
            ct);

        if (consumeResult.IsFailure)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        return Result.Success();
    }
}
