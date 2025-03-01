using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class ProcessGoogleCodeCommand : IRequest<Result<AuthTokensResult>>
{
    public required string Code { get; init; }
}