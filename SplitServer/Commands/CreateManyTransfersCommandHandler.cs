using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateManyTransfersCommandHandler : IRequestHandler<CreateManyTransfersCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;

    public CreateManyTransfersCommandHandler(
        ITransfersRepository transfersRepository,
        ValidationService validationService,
        PermissionService permissionService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result> Handle(CreateManyTransfersCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        foreach (var t in command.Transfers)
        {
            var transferValidationResult = _validationService.ValidateTransfer(group, t.SenderId, t.ReceiverId, t.Amount, t.Currency);

            if (transferValidationResult.IsFailure)
            {
                return transferValidationResult;
            }
        }

        var now = DateTime.UtcNow;

        var transfers = command.Transfers
            .Select(
                x => new Transfer
                {
                    Id = Guid.NewGuid().ToString(),
                    Created = now,
                    Updated = now,
                    GroupId = command.GroupId,
                    CreatorId = group.Members.Single(m => m.UserId == command.UserId).Id,
                    SenderId = x.SenderId,
                    ReceiverId = x.ReceiverId,
                    Amount = x.Amount,
                    Currency = x.Currency,
                    Description = x.Description,
                    Occurred = x.Occurred ?? now,
                })
            .ToList();

        return await _transfersRepository.InsertMany(transfers, ct);
    }
}