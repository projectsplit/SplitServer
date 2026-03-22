using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetNonGroupDebtsQuery : IRequest<Result<GetNonGroupDebtsResponse>>
{
    public required string UserId { get; init; }
    public string? SearchTerm { get; init; }
    public DateTime? After { get; init; }
    public DateTime? Before { get; init; }
    public string[]? ParticipantIds { get; init; }
    public string[]? PayerIds { get; init; }
    public string[]? LabelIds { get; init; }
    public string[]? ReceiverIds { get; init; }
    public string[]? SenderIds { get; init; }
}