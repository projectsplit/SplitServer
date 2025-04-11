using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Requests;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetLabelsQueryHandler : IRequestHandler<GetLabelsQuery, Result<GetLabelsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;

    public GetLabelsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        PermissionService permissionService)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _permissionService = permissionService;
    }

    public async Task<Result<GetLabelsResponse>> Handle(GetLabelsQuery query, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(query.UserId, query.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<GetLabelsResponse>();
        }

        var (_, group, _) = permissionResult.Value;

        var labelCounts = await _expensesRepository.GetLabelCounts(query.GroupId, ct);

        return new GetLabelsResponse
        {
            Labels = group.Labels
                .Select(
                    x => new LabelResponseItem
                    {
                        Id = x.Id,
                        Text = x.Text,
                        Color = x.Color,
                        Count = labelCounts.GetValueOrDefault(x.Id)
                    })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    // private static List<ExpenseLabelRequestItem> AutoCompleteSearch(Dictionary<Label, int> labelCountGroups, string? query)
    // {
    //     if (string.IsNullOrEmpty(query))
    //     {
    //         return labelCountGroups
    //             .Select(x => new ExpenseLabelRequestItem { Text = x.Key.Text, Color = x.Key.Color })
    //             .ToList();
    //     }
    //
    //     return labelCountGroups
    //         .Where(x => x.Key.Text.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
    //         .OrderBy(x => x.Key.Text.Length)
    //         .ThenBy(x => x.Value)
    //         .ThenBy(x => x.Key.Text)
    //         .Select(x => new ExpenseLabelRequestItem { Text = x.Key.Text, Color = x.Key.Color })
    //         .ToList();
    // }
}