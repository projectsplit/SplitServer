using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateManyNonGroupTransfersCommandHandler : IRequestHandler<CreateManyNonGroupTransfersCommand, Result>
{
    private readonly ITransfersRepository _transfersRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly ConnectionService _connectionService;
    private readonly PushNotificationService _pushNotificationService;

    public CreateManyNonGroupTransfersCommandHandler(
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

    public async Task<Result> Handle(CreateManyNonGroupTransfersCommand command, CancellationToken ct)
    {
        foreach (var t in command.Transfers)
        {
            var transferValidationResult = _validationService.ValidateNonGroupTransfer(
                t.SenderId,
                t.ReceiverId,
                command.UserId,
                t.Amount,
                t.Currency);

            if (transferValidationResult.IsFailure)
            {
                return transferValidationResult;
            }
        }

        var participantUserIds = command.Transfers
            .SelectMany(x => new[] { x.SenderId, x.ReceiverId })
            .Distinct()
            .ToList();

        var notConnectedUserIds = await _connectionService.GetNotConnectedUserIds(command.UserId, participantUserIds, ct);

        if (notConnectedUserIds.Count > 0)
        {
            var notConnectedUsers = await _usersRepository.GetByIds(notConnectedUserIds, ct);
            var usernames = string.Join(", ", notConnectedUsers.Select(x => x.Username));

            return Result.Failure($"You are not connected with: {usernames}. Send them a connection request first.");
        }

        var now = DateTime.UtcNow;

        var transfers = command.Transfers
            .Select(x => new NonGroupTransfer
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                CreatorId = command.UserId,
                SenderId = x.SenderId,
                ReceiverId = x.ReceiverId,
                Amount = x.Amount,
                Currency = x.Currency,
                Description = x.Description,
                Occurred = x.Occurred ?? now,
            })
            .ToList();

        var writeResult = await _transfersRepository.InsertMany(transfers, ct);

        if (writeResult.IsFailure)
        {
            return writeResult;
        }

        var creatorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var creatorUsername = creatorMaybe.HasValue ? creatorMaybe.Value.Username : "Someone";

        _pushNotificationService.NotifyInBackground(
            participantUserIds.Where(x => x != command.UserId),
            "Debt settled",
            $"{creatorUsername} settled up with you.",
            "/shared/nongroup/debts");

        return Result.Success();
    }
}
