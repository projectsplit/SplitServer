using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetCurrencyCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Currency { get; init; }
}