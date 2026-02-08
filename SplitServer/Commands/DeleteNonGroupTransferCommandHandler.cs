using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
namespace SplitServer.Commands;

public class DeleteNonGroupTransferCommandHandler: IRequestHandler<DeleteNonGroupTransferCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ITransfersRepository _transfersRepository;

    public DeleteNonGroupTransferCommandHandler(
        IUsersRepository usersRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _transfersRepository = transfersRepository;
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

        if (transfer is not NonGroupTransfer )
        {
            return Result.Failure($"Transfer with id {command.TransferId} was not found");
        }

        if (command.UserId != transfer.ReceiverId && command.UserId != transfer.SenderId)
        {
            return Result.Failure($"User {command.UserId} must be part of the non-group transfer");
        }
        
        return await _transfersRepository.Delete(command.TransferId, ct);
    } 
}