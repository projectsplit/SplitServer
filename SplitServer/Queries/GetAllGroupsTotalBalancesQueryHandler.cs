using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Repositories;

namespace SplitServer.Queries;

public class GetAllGroupsTotalBalancesQueryHandler : 
    IRequestHandler<GetAllGroupsTotalBalancesQuery, Result<GetAllGroupsTotalBalancesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetAllGroupsTotalBalancesQueryHandler(
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

    public async Task<Result<GetAllGroupsTotalBalancesResponse>> Handle(GetAllGroupsTotalBalancesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetAllGroupsTotalBalancesResponse>($"User with id {query.UserId} was not found");
        }

        var groups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var membersByGroup = groups.ToDictionary(x => x.Id, x => x.Members.First(m => m.UserId == query.UserId));
        var memberIds = membersByGroup.Select(m => m.Value.Id).ToList();
        
        var expenses = await _expensesRepository.GetAllByMemberIds(memberIds, ct);
        var transfers = await _transfersRepository.GetAllByMemberIds(memberIds, ct);
        
        var balanceByCurrency = new Dictionary<string, decimal>();

        foreach (var expense in expenses)
        {
            var memberId = membersByGroup[expense.GroupId].Id;
            
            var shareAmount = expense.Shares.FirstOrDefault(x => x.MemberId == memberId)?.Amount ?? 0;
            balanceByCurrency[expense.Currency] = balanceByCurrency.GetValueOrDefault(expense.Currency) + shareAmount;
            
            var paymentAmount = expense.Payments.FirstOrDefault(x => x.MemberId == memberId)?.Amount ?? 0;
            balanceByCurrency[expense.Currency] = balanceByCurrency.GetValueOrDefault(expense.Currency) - paymentAmount;
        }

        foreach (var transfer in transfers)
        {
            var memberId = membersByGroup[transfer.GroupId].Id;

            if (transfer.SenderId == memberId)
            {
                balanceByCurrency[transfer.Currency] = balanceByCurrency.GetValueOrDefault(transfer.Currency) - transfer.Amount;
            }

            if (transfer.ReceiverId == memberId)
            {
                balanceByCurrency[transfer.Currency] = balanceByCurrency.GetValueOrDefault(transfer.Currency) + transfer.Amount;
            }
        }

        return new GetAllGroupsTotalBalancesResponse
        {
            Balances = balanceByCurrency,
            GroupCount = groups.Count
        };
    }
}