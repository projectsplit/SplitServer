using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateNonGroupTransferCommandHandler : IRequestHandler<CreateNonGroupTransferCommand, Result<CreateTransferResponse>>
{
    private const string NonGroupTransfersUrl = "/shared/nongroup/transfers";

    private readonly ITransfersRepository _transfersRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly ConnectionService _connectionService;
    private readonly NotificationService _notificationService;

    public CreateNonGroupTransferCommandHandler(
        ITransfersRepository transfersRepository,
        IUsersRepository usersRepository,
        ValidationService validationService,
        ConnectionService connectionService,
        NotificationService notificationService)
    {
        _transfersRepository = transfersRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _connectionService = connectionService;
        _notificationService = notificationService;
    }

    public async Task<Result<CreateTransferResponse>> Handle(CreateNonGroupTransferCommand command, CancellationToken ct)
    {
        var transferValidationResult =
            _validationService.ValidateNonGroupTransfer(
                command.SenderId,
                command.ReceiverId,
                command.UserId,
                command.Amount,
                command.Currency);

        if (transferValidationResult.IsFailure)
        {
            return transferValidationResult.ConvertFailure<CreateTransferResponse>();
        }

        var connectionResult = await _connectionService.VerifyCanSplitWith(
            command.UserId,
            [command.SenderId, command.ReceiverId],
            ct);

        if (connectionResult.IsFailure)
        {
            return connectionResult.ConvertFailure<CreateTransferResponse>();
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

        await NotifyCounterparty(command, ct);

        return new CreateTransferResponse
        {
            TransferId = transferId
        };
    }

    /// <summary>
    /// Notifies the other side of the transfer. The creator is always one of the two parties here,
    /// and recording your own transfer is not news, so only the counterparty is told.
    /// </summary>
    private async Task NotifyCounterparty(CreateNonGroupTransferCommand command, CancellationToken ct)
    {
        string[] parties = [command.SenderId, command.ReceiverId];

        var recipientUserIds = parties
            .Where(x => x != command.UserId)
            .Distinct()
            .ToList();

        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var creatorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var creatorUsername = creatorMaybe.HasValue ? creatorMaybe.Value.Username : "Someone";

        await _notificationService.Notify(
            recipientUserIds,
            "New transfer",
            $"{creatorUsername} recorded a transfer of {command.Amount} {command.Currency} with you",
            NonGroupTransfersUrl,
            ct);
    }
}