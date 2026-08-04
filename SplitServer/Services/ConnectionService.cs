using CSharpFunctionalExtensions;
using SplitServer.Repositories;

namespace SplitServer.Services;

/// <summary>
/// Decides who a user is allowed to put on a non-group expense or transfer. Anyone can be found
/// by search, but only people you have some existing relationship with can be split with; the
/// rest have to accept a connection request first.
/// </summary>
public class ConnectionService
{
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public ConnectionService(
        IUserConnectionsRepository userConnectionsRepository,
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _userConnectionsRepository = userConnectionsRepository;
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
    }

    /// <summary>
    /// Two users count as connected when they have an accepted connection request, are members of
    /// a common group, or already share non-group expense or transfer history. The last two mean
    /// everyone who was splitting with someone before connections existed keeps being able to,
    /// without a migration and without having to re-request people they clearly already know.
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

    /// <summary>
    /// The ones out of <paramref name="otherUserIds"/> that <paramref name="userId"/> may not
    /// split with. The user themselves is never in the result: you are always allowed on your own
    /// expenses.
    /// </summary>
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

    /// <summary>
    /// The guard every non-group write goes through. Names the users that are in the way rather
    /// than just refusing, because the client sends a whole expense at once and the person has to
    /// know which of the people on it to send a request to.
    /// </summary>
    public async Task<Result> VerifyCanSplitWith(
        string userId,
        IEnumerable<string> otherUserIds,
        CancellationToken ct)
    {
        var notConnectedUserIds = await GetNotConnectedUserIds(userId, otherUserIds, ct);

        if (notConnectedUserIds.Count == 0)
        {
            return Result.Success();
        }

        var notConnectedUsers = await _usersRepository.GetByIds(notConnectedUserIds, ct);

        var usernames = string.Join(", ", notConnectedUsers.Select(x => x.Username));

        return Result.Failure($"You are not connected with: {usernames}. Send them a connection request first");
    }
}
