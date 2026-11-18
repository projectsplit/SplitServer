using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Queries;

/// <summary>
/// What Plus the app may offer for sale, if any. The same for everybody: whether this person
/// already has Plus travels with their own details, not with the offer.
/// </summary>
public class GetPlusOfferQueryHandler : IRequestHandler<GetPlusOfferQuery, Result<GetPlusOfferResponse>>
{
    private readonly IPlaySubscriptions _play;
    private readonly PlusCatalog _catalog;

    public GetPlusOfferQueryHandler(IPlaySubscriptions play, PlusCatalog catalog)
    {
        _play = play;
        _catalog = catalog;
    }

    public Task<Result<GetPlusOfferResponse>> Handle(GetPlusOfferQuery query, CancellationToken ct)
    {
        var isAvailable = _catalog.IsOffered(_play.IsConfigured);

        var response = new GetPlusOfferResponse
        {
            IsAvailable = isAvailable,
            ProductId = _catalog.ProductId,
            // Empty rather than present-but-unbuyable when Plus is not on offer, so a client that
            // forgets to check the flag still has nothing to draw.
            Plans = isAvailable
                ? _catalog.Plans
                    .Select(
                        x => new PlusPlanResponse
                        {
                            Plan = PlusCatalog.NameOf(x.Plan),
                            BasePlanId = x.BasePlanId,
                        })
                    .ToArray()
                : [],
        };

        return Task.FromResult(Result.Success(response));
    }
}
