using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateManyTransfersCommandHandler : IRequestHandler<CreateManyTransfersCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;

    public CreateManyTransfersCommandHandler(
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

    public async Task<Result> Handle(CreateManyTransfersCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        var creatorMemberId = group.Members.FirstOrDefault(m => m.UserId == command.UserId)?.Id;

        if (creatorMemberId is null)
        {
            return Result.Failure("User must be a group member");
        }

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
                    IsDeleted = false,
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