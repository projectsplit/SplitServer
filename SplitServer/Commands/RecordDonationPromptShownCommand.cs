using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RecordDonationPromptShownCommand : IRequest<Result>
{
    public required string UserId { get; init; }
}
