using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetShowBudgetInfoCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required bool ShowBudgetInfo { get; init; }
}