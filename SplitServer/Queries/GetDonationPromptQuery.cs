using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetDonationPromptQuery : IRequest<Result<GetDonationPromptResponse>>
{
    public required string UserId { get; init; }
}
