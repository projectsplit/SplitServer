using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class RefreshCommand : IRequest<Result<AuthTokensResult>>
{
    public string RefreshToken { get; }

    public RefreshCommand(string refreshToken)
    {
        RefreshToken = refreshToken;
    }
}