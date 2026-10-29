using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Serilog;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

/// <summary>
/// Changes the password of a signed-in account that has one. Knowing the current password is what
/// proves the request comes from the account holder rather than from whoever picked up an unlocked
/// phone, so nothing changes until it has been checked. Every other session is signed out afterwards:
/// a password is usually changed because the old one may have leaked, and a leaked password buys
/// nothing if the sessions it opened do not outlive it. The session that made the change stays.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    public const string NoPasswordError = "This account signs in with Google and has no password to change";
    public const string WrongCurrentPasswordError = "Current password is incorrect";
    public const string SamePasswordError = "New password must be different from your current password";

    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ValidationService _validationService;

    public ChangePasswordCommandHandler(
        IUsersRepository usersRepository,
        ISessionsRepository sessionsRepository,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _sessionsRepository = sessionsRepository;
        _validationService = validationService;
    }

    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var passwordValidationResult = _validationService.ValidatePassword(command.NewPassword);

        if (passwordValidationResult.IsFailure)
        {
            return passwordValidationResult;
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var user = userMaybe.Value;

        // An account created through Google has nothing to check the current password against, and
        // the app hides this option from it; the refusal is for a request that got here anyway.
        if (user.HashedPassword is null)
        {
            return Result.Failure(NoPasswordError);
        }

        var hasher = new PasswordHasher<string>();

        // A missing current password is a wrong one, not an argument for the hasher to throw on.
        if (string.IsNullOrEmpty(command.CurrentPassword) ||
            hasher.VerifyHashedPassword(user.Id, user.HashedPassword, command.CurrentPassword) is PasswordVerificationResult.Failed)
        {
            return Result.Failure(WrongCurrentPasswordError);
        }

        if (command.NewPassword == command.CurrentPassword)
        {
            return Result.Failure(SamePasswordError);
        }

        var now = DateTime.UtcNow;

        var updateResult = await _usersRepository.Update(
            user with
            {
                HashedPassword = hasher.HashPassword(user.Id, command.NewPassword),
                Updated = now,
            },
            ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        // The password has changed whatever happens next, so a failure here is not reported as one:
        // telling the user the change failed when it did not would only have them retry with a
        // "current" password that no longer is.
        var signOutResult = await _sessionsRepository.DeleteByUserIdExceptSession(user.Id, command.SessionId, ct);

        if (signOutResult.IsFailure)
        {
            Log.Warning("Failed to sign out the other sessions of user {UserId} after a password change", user.Id);
        }

        return Result.Success();
    }
}
