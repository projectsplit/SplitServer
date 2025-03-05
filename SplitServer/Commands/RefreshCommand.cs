using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class RefreshCommand : IRequest<Result<AuthenticationResponse>>
{
    public required string RefreshToken { get; init; }
}