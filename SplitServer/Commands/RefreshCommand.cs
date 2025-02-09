using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class RefreshCommand : IRequest<Result<AuthTokensResult>>
{
    public required string RefreshToken { get; init; }
}