using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditTransferCommandHandler : IRequestHandler<EditTransferCommand, Result>
{
    private readonly ITransfersRepository _transfersRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;

    public EditTransferCommandHandler(
        ITransfersRepository transfersRepository,
        PermissionService permissionService,
        ValidationService validationService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result> Handle(EditTransferCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyTransferAction(command.UserId, command.TransferId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, transfer, _) = permissionResult.Value;

        var transferValidationResult =
            _validationService.ValidateTransfer(group, command.SenderId, command.ReceiverId, command.Amount, command.Currency);

        if (transferValidationResult.IsFailure)
        {
            return transferValidationResult;
        }

        var now = DateTime.UtcNow;

        var editedTransfer = transfer with
        {
            Updated = now,
            SenderId = command.SenderId,
            ReceiverId = command.ReceiverId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency
        };

        return await _transfersRepository.Update(editedTransfer, ct);
    }
}