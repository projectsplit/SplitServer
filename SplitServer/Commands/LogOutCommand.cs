using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class LogOutCommand : IRequest<Result>
{
    public required string RefreshToken { get; init; }
}