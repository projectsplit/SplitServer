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
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly ConnectionService _connectionService;
    private readonly PushNotificationService _pushNotificationService;

    public CreateNonGroupTransferCommandHandler(
        ITransfersRepository transfersRepository,
        IUsersRepository usersRepository,
        ValidationService validationService,
        ConnectionService connectionService,
        PushNotificationService pushNotificationService)
    {
        _transfersRepository = transfersRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _connectionService = connectionService;
        _pushNotificationService = pushNotificationService;
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

        var participantUserIds = new List<string> { command.SenderId, command.ReceiverId };

        var notConnectedUserIds = await _connectionService.GetNotConnectedUserIds(command.UserId, participantUserIds, ct);

        if (notConnectedUserIds.Count > 0)
        {
            var notConnectedUsers = await _usersRepository.GetByIds(notConnectedUserIds, ct);
            var usernames = string.Join(", ", notConnectedUsers.Select(x => x.Username));

            return Result.Failure<CreateTransferResponse>(
                $"You are not connected with: {usernames}. Send them a connection request first.");
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

        var creatorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var creatorUsername = creatorMaybe.HasValue ? creatorMaybe.Value.Username : "Someone";

        _pushNotificationService.NotifyInBackground(
            participantUserIds.Where(x => x != command.UserId),
            "New transfer",
            $"{creatorUsername} recorded a transfer of {command.Amount} {command.Currency}.",
            "/shared/nongroup/transfers");

        return new CreateTransferResponse
        {
            TransferId = transferId
        };
    }
}
