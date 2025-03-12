using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.Auth;

namespace SplitServer.Commands;

public class LogOutCommandHandler : IRequestHandler<LogOutCommand, Result>
{
    private readonly ISessionsRepository _sessionsRepository;

    public LogOutCommandHandler(
        ISessionsRepository sessionsRepository,
        AuthService authService)
    {
        _sessionsRepository = sessionsRepository;
    }

    public async Task<Result> Handle(LogOutCommand command, CancellationToken ct)
    {
        return await _sessionsRepository.DeleteByRefreshToken(command.RefreshToken, ct);
    }
}