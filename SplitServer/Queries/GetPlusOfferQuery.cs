using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetPlusOfferQuery : IRequest<Result<GetPlusOfferResponse>>
{
}
