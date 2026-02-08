using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateNonGroupTransferCommandHandler : IRequestHandler<CreateNonGroupTransferCommand, Result<CreateTransferResponse>>
{
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;

    public CreateNonGroupTransferCommandHandler(
        ITransfersRepository transfersRepository,
        ValidationService validationService
        )
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
    }

    public async Task<Result<CreateTransferResponse>> Handle(CreateNonGroupTransferCommand command, CancellationToken ct)
    {
        var transferValidationResult =
            _validationService.ValidateNonGroupTransfer( command.SenderId, command.ReceiverId,command.UserId, command.Amount, command.Currency);

        if (transferValidationResult.IsFailure)
        {
            return transferValidationResult.ConvertFailure<CreateTransferResponse>();
        }

        var now = DateTime.UtcNow;
        var transferId = Guid.NewGuid().ToString();

        var newTransfer = new NonGroupTransfer
        {
            Id = transferId,
            Created = now,
            Updated = now,
            CreatorId = command.UserId,
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