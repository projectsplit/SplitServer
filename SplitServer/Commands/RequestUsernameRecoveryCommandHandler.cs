using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Repositories;
using SplitServer.Services.Email;

namespace SplitServer.Commands;

public class RequestUsernameRecoveryCommandHandler : IRequestHandler<RequestUsernameRecoveryCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IEmailSender _emailSender;

    public RequestUsernameRecoveryCommandHandler(
        IUsersRepository usersRepository,
        IEmailSender emailSender)
    {
        _usersRepository = usersRepository;
        _emailSender = emailSender;
    }

    public async Task<Result> Handle(RequestUsernameRecoveryCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Success();
        }

        var userMaybe = await _usersRepository.GetByEmail(command.Email, ct);

        if (userMaybe.HasNoValue || !userMaybe.Value.EmailVerified)
        {
            return Result.Success();
        }

        var user = userMaybe.Value;

        var body =
            $"Hi,\n\n" +
            $"Your username is: {user.Username}\n\n" +
            "If you did not request this, you can ignore this email.";

        var sendResult = await _emailSender.SendAsync(user.Email!, "Your username", body, ct);

        if (sendResult.IsFailure)
        {
            Log.Warning("Failed to send username recovery email for user {UserId}: {Error}", user.Id, sendResult.Error);
        }

        return Result.Success();
    }
}
