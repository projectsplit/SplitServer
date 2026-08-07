using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class ProcessGoogleIdTokenCommand : IRequest<Result<AuthenticationResponse>>
{
    public required string IdToken { get; init; }
}
