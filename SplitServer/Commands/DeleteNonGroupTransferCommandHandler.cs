using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class DeleteNonGroupTransferCommandHandler : IRequestHandler<DeleteNonGroupTransferCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private const string NonGroupTransfersUrl = "/shared/nongroup/transfers";

    private readonly ITransfersRepository _transfersRepository;
    private readonly NotificationService _notificationService;

    public DeleteNonGroupTransferCommandHandler(
        IUsersRepository usersRepository,
        ITransfersRepository transfersRepository,
        NotificationService notificationService)
    {
        _usersRepository = usersRepository;
        _transfersRepository = transfersRepository;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(DeleteNonGroupTransferCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var transferMaybe = await _transfersRepository.GetById(command.TransferId, ct);

        if (transferMaybe.HasNoValue)
        {
            return Result.Failure($"Transfer with id {command.TransferId} was not found");
        }

        var transfer = transferMaybe.Value;

        if (transfer is not NonGroupTransfer)
        {
            return Result.Failure($"Transfer with id {command.TransferId} was not found");
        }

        if (command.UserId != transfer.ReceiverId && command.UserId != transfer.SenderId)
        {
            return Result.Failure($"User {command.UserId} must be part of the non-group transfer");
        }

        var deleteResult = await _transfersRepository.Delete(command.TransferId, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        var recipientUserIds = new[] { transfer.SenderId, transfer.ReceiverId }
            .Where(x => x != command.UserId)
            .Distinct()
            .ToList();

        await _notificationService.Notify(
            recipientUserIds,
            "Transfer deleted",
            $"{userMaybe.Value.Username} deleted a transfer of {transfer.Amount} {transfer.Currency}",
            NonGroupTransfersUrl,
            ct);

        return deleteResult;
    }
}