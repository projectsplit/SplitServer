using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditUsernameCommandHandler : IRequestHandler<EditUsernameCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly LockService _lockService;
    private readonly ValidationService _validationService;

    public EditUsernameCommandHandler(
        IUsersRepository usersRepository,
        LockService lockService,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _lockService = lockService;
        _validationService = validationService;
    }

    public async Task<Result> Handle(EditUsernameCommand command, CancellationToken ct)
    {
        using var _ = _lockService.AcquireLock(command.Username);

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var user = userMaybe.Value;

        var usernameValidationResult = _validationService.ValidateUsername(command.Username);

        if (usernameValidationResult.IsFailure)
        {
            return usernameValidationResult;
        }

        var userByUsernameMaybe = await _usersRepository.GetByUsername(command.Username, ct);

        if (userByUsernameMaybe.HasValue)
        {
            return Result.Failure("Username is already taken");
        }

        return await _usersRepository.Update(
            user with
            {
                Username = command.Username,
                Updated = DateTime.UtcNow
            },
            ct);
    }
}