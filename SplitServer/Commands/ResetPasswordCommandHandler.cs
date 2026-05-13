using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Email;

namespace SplitServer.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private const string InvalidOrExpiredError = "Invalid or expired token";

    private readonly IUsersRepository _usersRepository;
    private readonly IEmailVerificationCodesRepository _codesRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly EmailTokenService _tokenService;

    public ResetPasswordCommandHandler(
        IUsersRepository usersRepository,
        IEmailVerificationCodesRepository codesRepository,
        ISessionsRepository sessionsRepository,
        EmailTokenService tokenService)
    {
        _usersRepository = usersRepository;
        _codesRepository = codesRepository;
        _sessionsRepository = sessionsRepository;
        _tokenService = tokenService;
    }

    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Token) || string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var tokenHash = _tokenService.Hash(command.Token);

        var codeMaybe = await _codesRepository.GetActiveByCodeHash(
            tokenHash,
            EmailVerificationCodePurpose.PasswordReset,
            ct);

        if (codeMaybe.HasNoValue)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        var code = codeMaybe.Value;
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

        var hasher = new PasswordHasher<string>();
        var newHashedPassword = hasher.HashPassword(user.Id, command.NewPassword);

        var updateUserResult = await _usersRepository.Update(
            user with
            {
                HashedPassword = newHashedPassword,
                Updated = now
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
                Updated = now
            },
            ct);

        if (consumeResult.IsFailure)
        {
            return Result.Failure(InvalidOrExpiredError);
        }

        await _sessionsRepository.DeleteByUserId(user.Id, ct);

        return Result.Success();
    }
}
