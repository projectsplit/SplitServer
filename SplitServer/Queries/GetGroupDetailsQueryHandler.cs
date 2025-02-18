using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Repositories;

namespace SplitServer.Queries;

public class GetGroupDetailsQueryHandler : IRequestHandler<GetGroupDetailsQuery, Result<GetGroupDetailsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetGroupDetailsQueryHandler(
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

    public async Task<Result<GetGroupDetailsResponse>> Handle(GetGroupDetailsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupDetailsResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupDetailsResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupDetailsResponse>("User is not a group member");
        }

        var memberId = group.Members.First(m => m.UserId == query.UserId).Id;

        var expenses = await _expensesRepository.GetAllByMemberIds([memberId], ct);
        var transfers = await _transfersRepository.GetAllByMemberIds([memberId], ct);

        var groupDetails = new Dictionary<string, decimal>();

        foreach (var transfer in transfers)
        {
            if (transfer.SenderId == memberId)
            {
                groupDetails[transfer.Currency] = groupDetails.GetValueOrDefault(transfer.Currency) + transfer.Amount;
            }

            if (transfer.ReceiverId == memberId)
            {
                groupDetails[transfer.Currency] = groupDetails.GetValueOrDefault(transfer.Currency) - transfer.Amount;
            }
        }

        foreach (var expense in expenses.Where(x => x.GroupId == group.Id))
        {
            var payment = expense.Payments.FirstOrDefault(x => x.MemberId == memberId);
            if (payment is not null)
            {
                groupDetails[expense.Currency] = groupDetails.GetValueOrDefault(expense.Currency) + payment.Amount;
            }

            var share = expense.Shares.FirstOrDefault(x => x.MemberId == memberId);
            if (share is not null)
            {
                groupDetails[expense.Currency] = groupDetails.GetValueOrDefault(expense.Currency) - share.Amount;
            }
        }

        return new GetGroupDetailsResponse
        {
            Id = group.Id,
            Name = group.Name,
            Details = groupDetails,
        };
    }
}