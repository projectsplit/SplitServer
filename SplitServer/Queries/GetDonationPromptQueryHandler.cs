using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.Donations;

namespace SplitServer.Queries;

public class GetDonationPromptQueryHandler : IRequestHandler<GetDonationPromptQuery, Result<GetDonationPromptResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IDonationPromptStatesRepository _donationPromptStatesRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly DonationPromptPolicy _policy;
    private readonly GooglePlayBillingService _play;
    private readonly DonationCatalog _catalog;

    public GetDonationPromptQueryHandler(
        IUsersRepository usersRepository,
        IDonationPromptStatesRepository donationPromptStatesRepository,
        IExpensesRepository expensesRepository,
        DonationPromptPolicy policy,
        GooglePlayBillingService play,
        DonationCatalog catalog)
    {
        _usersRepository = usersRepository;
        _donationPromptStatesRepository = donationPromptStatesRepository;
        _expensesRepository = expensesRepository;
        _policy = policy;
        _play = play;
        _catalog = catalog;
    }

    public async Task<Result<GetDonationPromptResponse>> Handle(GetDonationPromptQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetDonationPromptResponse>($"User with id {query.UserId} was not found");
        }

        var now = DateTime.UtcNow;

        var stateMaybe = await _donationPromptStatesRepository.GetById(query.UserId, ct);
        var state = stateMaybe.GetValueOrDefault(() => DonationPromptState.CreateEmpty(query.UserId, now));

        var block = _play.IsConfigured
            ? _policy.EvaluateWithoutEngagement(state, userMaybe.Value.Created, now)
            : DonationPromptBlock.NotConfigured;

        // The expense count is the one gate that costs a query, so it runs last and only for someone
        // already through everything else — at most once per person per cooldown window, and never
        // again once they pass. A pass is written back so the count is not repeated.
        if (block == DonationPromptBlock.None && _policy.NeedsEngagementCheck(state))
        {
            var expenseCount = await _expensesRepository.CountByCreatorId(
                query.UserId,
                _policy.MinExpensesCreated,
                ct);

            if (expenseCount < _policy.MinExpensesCreated)
            {
                block = DonationPromptBlock.NotEngagedEnough;
            }
            else
            {
                state = state with { EngagementReachedAt = now, Updated = now };

                // Best-effort: a failed write only means the count runs again next time.
                await _donationPromptStatesRepository.Upsert(state, ct);
            }
        }

        return new GetDonationPromptResponse
        {
            ShouldAsk = block == DonationPromptBlock.None,
            IsAvailable = _play.IsConfigured,
            Products = _catalog.Products
                .Select(
                    x => new DonationProductResponse
                    {
                        ProductId = x.ProductId,
                        Kind = (int)x.Kind,
                        BasePlanId = x.BasePlanId,
                    })
                .ToArray(),
            HasDonated = state.LastDonatedAt is not null,
            HasActiveMonthly = state.HasActiveMonthly,
        };
    }
}
