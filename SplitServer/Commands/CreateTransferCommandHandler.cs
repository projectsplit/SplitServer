using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, Result<CreateTransferResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;

    public CreateTransferCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        ITransfersRepository transfersRepository,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _transfersRepository = transfersRepository;
        _validationService = validationService;
    }

    public async Task<Result<CreateTransferResponse>> Handle(CreateTransferCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateTransferResponse>($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<CreateTransferResponse>($"Group with id {command.GroupId} was not found");
        }

        var amountValidationResult = _validationService.ValidateAmount(command.Amount, command.Currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult.ConvertFailure<CreateTransferResponse>();
        }

        var group = groupMaybe.Value;

        var creatorMemberId = group.Members.FirstOrDefault(m => m.UserId == command.UserId)?.Id;

        if (creatorMemberId is null)
        {
            return Result.Failure<CreateTransferResponse>("User must be a group member");
        }

        if (group.Members.All(x => x.Id != command.SenderId))
        {
            return Result.Failure<CreateTransferResponse>("Sender must be a group member");
        }

        if (group.Members.All(x => x.Id != command.ReceiverId))
        {
            return Result.Failure<CreateTransferResponse>("Receiver must be a group member");
        }

        if (command.SenderId == command.ReceiverId)
        {
            return Result.Failure<CreateTransferResponse>("Receiver must be different from sender");
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
            CreatorId = creatorMemberId,
            SenderId = command.SenderId,
            ReceiverId = command.ReceiverId,
            Amount = command.Amount,
            Occured = command.Occured ?? now,
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