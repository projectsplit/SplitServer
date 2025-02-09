using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class ProcessGoogleAccessTokenCommand : IRequest<Result<AuthTokensResult>>
{
    public required string GoogleAccessToken { get; init; }
}