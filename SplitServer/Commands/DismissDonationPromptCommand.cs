using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DismissDonationPromptCommand : IRequest<Result>
{
    public required string UserId { get; init; }

    /// <summary>True for "don't ask again". False is an ordinary "not now".</summary>
    public required bool OptOut { get; init; }
}
