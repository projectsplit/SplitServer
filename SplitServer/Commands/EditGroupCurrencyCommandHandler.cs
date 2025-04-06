using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditGroupCurrencyCommandHandler : IRequestHandler<EditGroupCurrencyCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly IGroupsRepository _groupsRepository;

    public EditGroupCurrencyCommandHandler(
        PermissionService permissionService,
        ValidationService validationService,
        IGroupsRepository groupsRepository)
    {
        _permissionService = permissionService;
        _validationService = validationService;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(EditGroupCurrencyCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        var currencyValidationResult = _validationService.ValidateCurrency(command.Currency);

        if (currencyValidationResult.IsFailure)
        {
            return currencyValidationResult;
        }

        var updatedGroup = group with
        {
            Currency = command.Currency,
            Updated = DateTime.UtcNow
        };

        return await _groupsRepository.Update(updatedGroup, ct);
    }
}