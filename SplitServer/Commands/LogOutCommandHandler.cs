using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Repositories;
using SplitServer.Services.Auth;

namespace SplitServer.Commands;

public class LogOutCommandHandler : IRequestHandler<LogOutCommand, Result>
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IDiagnosticContext _diagnosticContext;

    public LogOutCommandHandler(
        ISessionsRepository sessionsRepository,
        AuthService authService,
        IDiagnosticContext diagnosticContext)
    {
        _sessionsRepository = sessionsRepository;
        _diagnosticContext = diagnosticContext;
    }

    public async Task<Result> Handle(LogOutCommand command, CancellationToken ct)
    {
        var sessionMaybe = await _sessionsRepository.GetByRefreshToken(command.RefreshToken, ct);

        if (sessionMaybe.HasValue)
        {
            _diagnosticContext.Set("UserId", sessionMaybe.Value.UserId);
        }

        return await _sessionsRepository.DeleteByRefreshToken(command.RefreshToken, ct);
    }
}