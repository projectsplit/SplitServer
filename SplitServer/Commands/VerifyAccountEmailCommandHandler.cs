using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Email;

namespace SplitServer.Commands;

public class VerifyAccountEmailCommandHandler : IRequestHandler<VerifyAccountEmailCommand, Result>
{
    private const string InvalidOrExpiredError = "Invalid or expired code";

    private readonly IUsersRepository _usersRepository;
    private readonly IEmailVerificationCodesRepository _codesRepository;
    private readonly EmailTokenService _tokenService;

    public VerifyAccountEmailCommandHandler(
        IUsersRepository usersRepository,
        IEmailVerificationCodesRepository codesRepository,
        EmailTokenService tokenService)
    {
        _usersRepository = usersRepository;
        _codesRepository = codesRepository;
        _tokenService = tokenService;
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
