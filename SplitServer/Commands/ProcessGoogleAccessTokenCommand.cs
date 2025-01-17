using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class ProcessGoogleAccessTokenCommand : IRequest<Result<AuthTokensResult>>
{
    public string GoogleAccessToken { get; }

    public ProcessGoogleAccessTokenCommand(string googleAccessToken)
    {
        GoogleAccessToken = googleAccessToken;
    }
}