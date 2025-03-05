using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class ProcessGoogleCodeCommand : IRequest<Result<AuthenticationResponse>>
{
    public required string Code { get; init; }
}