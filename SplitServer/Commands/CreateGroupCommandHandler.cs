using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, Result<CreateGroupResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ValidationService _validationService;

    public CreateGroupCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _validationService = validationService;
    }

    public async Task<Result<CreateGroupResponse>> Handle(CreateGroupCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateGroupResponse>($"User with id {command.UserId} was not found");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result.Failure<CreateGroupResponse>("Group name cannot be null or empty");
        }

        var currencyValidationResult = _validationService.ValidateCurrency(command.Currency);

        if (currencyValidationResult.IsFailure)
        {
            return Result.Failure<CreateGroupResponse>(currencyValidationResult.Error);
        }

        var now = DateTime.UtcNow;
        var groupId = Guid.NewGuid().ToString();

        var ownerMember = new Member
        {
            Id = Guid.NewGuid().ToString(),
            UserId = command.UserId,
            Joined = now
        };

        var newGroup = new Group
        {
            Id = groupId,
            Created = now,
            Updated = now,
            OwnerId = command.UserId,
            Name = command.Name,
            Currency = command.Currency,
            IsArchived = false,
            Members = [ownerMember],
            Guests = [],
            Labels = [],
        };

        var writeResult = await _groupsRepository.Insert(newGroup, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateGroupResponse>();
        }

        return new CreateGroupResponse
        {
            GroupId = groupId
        };
    }
}