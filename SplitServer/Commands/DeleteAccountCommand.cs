using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteAccountCommand : IRequest<Result>
{
    public required string UserId { get; init; }
}
