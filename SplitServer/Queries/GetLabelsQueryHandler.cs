using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Requests;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetLabelsQueryHandler : IRequestHandler<GetLabelsQuery, Result<GetLabelsResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;

    public GetLabelsQueryHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService)
    {
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
}