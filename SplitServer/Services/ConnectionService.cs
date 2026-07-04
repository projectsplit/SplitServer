using SplitServer.Repositories;

namespace SplitServer.Services;

public class ConnectionService
{
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public ConnectionService(
        IUserConnectionsRepository userConnectionsRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _userConnectionsRepository = userConnectionsRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
    }

    /// <summary>
    /// Two users are considered connected when they have an accepted connection request,
    /// are members of a common group, or already share non-group expense/transfer history.
    /// </summary>
    public async Task<HashSet<string>> GetConnectedUserIds(string userId, CancellationToken ct)
    {
        var acceptedUserIds = await _userConnectionsRepository.GetAcceptedUserIds(userId, ct);
        var groups = await _groupsRepository.GetAllByUserId(userId, ct);
        var expenseUserIds = await _expensesRepository.GetNonGroupUserIdsByUserId(userId, ct);
        var transferUserIds = await _transfersRepository.GetNonGroupUserIdsByUserId(userId, ct);

        var groupMemberUserIds = groups.SelectMany(g => g.Members.Select(m => m.UserId));

        return acceptedUserIds
            .Concat(groupMemberUserIds)
            .Concat(expenseUserIds)
            .Concat(transferUserIds)
            .Where(x => x != userId)
            .ToHashSet();
    }

    public async Task<List<string>> GetNotConnectedUserIds(
        string userId,
        IEnumerable<string> otherUserIds,
        CancellationToken ct)
    {
        var others = otherUserIds
            .Where(x => x != userId)
            .Distinct()
            .ToList();

        if (others.Count == 0)
        {
            return [];
        }

        var connectedUserIds = await GetConnectedUserIds(userId, ct);

        return others.Where(x => !connectedUserIds.Contains(x)).ToList();
    }
}
