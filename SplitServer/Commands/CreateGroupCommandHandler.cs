using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, Result<CreateGroupResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public CreateGroupCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<CreateGroupResponse>> Handle(CreateGroupCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateGroupResponse>($"User with id {command.UserId} was not found");
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
            Members = [ownerMember],
            Guests = [],
            Labels = [],
            IsDeleted = false
        };
        
        var writeResult = await _groupsRepository.Insert(newGroup, ct);

        if (writeResult.IsFailure)
        {
            return Result.Failure<CreateGroupResponse>($"Failed to create group: {writeResult.Error}");
        }

        return new CreateGroupResponse
        {
            GroupId = groupId
        };
    }
}