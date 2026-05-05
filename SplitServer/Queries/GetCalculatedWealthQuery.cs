using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetCalculatedWealthQuery : IRequest<Result<GetCalculatedWealthResponse>>
{
    public required string UserId { get; init; }
}
