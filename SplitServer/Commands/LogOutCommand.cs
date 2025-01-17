using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class LogOutCommand : IRequest<Result>
{
    public string RefreshToken { get; }

    public LogOutCommand(string refreshToken)
    {
        RefreshToken = refreshToken;
    }
}