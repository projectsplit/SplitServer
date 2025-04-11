using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class RemoveGroupLabelCommandHandler : IRequestHandler<RemoveGroupLabelCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;

    public RemoveGroupLabelCommandHandler(
        PermissionService permissionService,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result> Handle(RemoveGroupLabelCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        if (group.Labels.All(x => x.Id != command.LabelId))
        {
            return Result.Failure("Label does not exist");
        }

        if (await _expensesRepository.LabelIsInUse(command.GroupId, command.LabelId, ct))
        {
            return Result.Failure("Label is in use");
        }

        return await _groupsRepository.Update(
            group with
            {
                Labels = group.Labels.Where(x => x.Id != command.LabelId).ToList(),
                Updated = DateTime.UtcNow
            },
            ct);
    }
}