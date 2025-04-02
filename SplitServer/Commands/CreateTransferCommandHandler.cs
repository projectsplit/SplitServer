using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, Result<CreateTransferResponse>>
{
    private readonly PermissionService _permissionService;
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;

    public CreateTransferCommandHandler(
        ITransfersRepository transfersRepository,
        ValidationService validationService,
        PermissionService permissionService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
    }

    public async Task<Result<CreateTransferResponse>> Handle(CreateTransferCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateTransferResponse>();
        }

        var (_, group, memberId) = permissionResult.Value;

        var transferValidationResult =
            _validationService.ValidateTransfer(group, command.SenderId, command.ReceiverId, command.Amount, command.Currency);

        if (transferValidationResult.IsFailure)
        {
            return transferValidationResult.ConvertFailure<CreateTransferResponse>();
        }

        var now = DateTime.UtcNow;
        var transferId = Guid.NewGuid().ToString();

        var newTransfer = new Transfer
        {
            Id = transferId,
            IsDeleted = false,
            Created = now,
            Updated = now,
            GroupId = command.GroupId,
            CreatorId = memberId,
            SenderId = command.SenderId,
            ReceiverId = command.ReceiverId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency
        };

        var writeResult = await _transfersRepository.Insert(newTransfer, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateTransferResponse>();
        }

        return new CreateTransferResponse
        {
            TransferId = transferId
        };
    }
}