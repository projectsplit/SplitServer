using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupsWithDetailsQueryHandler : IRequestHandler<GetGroupsWithDetailsQuery, Result<GetGroupsWithDetailsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetGroupsWithDetailsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
    }

    public async Task<Result<GetGroupsWithDetailsResponse>> Handle(GetGroupsWithDetailsQuery query, CancellationToken ct)
    {
        if (query.PageSize < 1)
        {
            return Result.Failure<GetGroupsWithDetailsResponse>("Page size must be greater than 0");
        }

        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupsWithDetailsResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = Next.Parse<NextGroupPageDetails>(query.Next);

        var groups = await _groupsRepository.GetByUserId(query.UserId, query.PageSize, nextDetails?.Created, ct);
        var userMemberIds = groups.Select(g => g.Members.First(m => m.UserId == query.UserId)).Select(m => m.Id).ToList();

        var expenses = await _expensesRepository.GetAllByMemberIds(userMemberIds, ct);
        var transfers = await _transfersRepository.GetAllByMemberIds(userMemberIds, ct);

        var groupDetails = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var group in groups)
        {
            var memberId = group.Members.First(m => m.UserId == query.UserId).Id;
            groupDetails[group.Id] = new Dictionary<string, decimal>();

            foreach (var transfer in transfers.Where(x => x.GroupId == group.Id))
            {
                if (transfer.SenderId == memberId)
                {
                    groupDetails[group.Id][transfer.Currency] = groupDetails[group.Id].GetValueOrDefault(transfer.Currency) + transfer.Amount;
                }

                if (transfer.ReceiverId == memberId)
                {
                    groupDetails[group.Id][transfer.Currency] = groupDetails[group.Id].GetValueOrDefault(transfer.Currency) - transfer.Amount;
                }
            }

            foreach (var expense in expenses.Where(x => x.GroupId == group.Id))
            {
                var payment = expense.Payments.FirstOrDefault(x => x.MemberId == memberId);
                if (payment is not null)
                {
                    groupDetails[group.Id][expense.Currency] = groupDetails[group.Id].GetValueOrDefault(expense.Currency) + payment.Amount;
                }

                var share = expense.Shares.FirstOrDefault(x => x.MemberId == memberId);
                if (share is not null)
                {
                    groupDetails[group.Id][expense.Currency] = groupDetails[group.Id].GetValueOrDefault(expense.Currency) - share.Amount;
                }
            }
        }

        return new GetGroupsWithDetailsResponse
        {
            Groups = groups.Select(
                x => new GetGroupsWithDetailsResponseItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    Details = groupDetails[x.Id],
                }).ToList(),
            Next = GetNext(query, groups)
        };
    }

    private static string? GetNext(GetGroupsWithDetailsQuery query, List<Group> groups)
    {
        return Next.Create(groups, query.PageSize, x => new NextGroupPageDetails { Created = x.Last().Created });
    }
}

file class NextGroupPageDetails
{
    public required DateTime Created { get; init; }
}