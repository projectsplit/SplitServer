using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class AddGuestCommandHandler : IRequestHandler<AddGuestCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public AddGuestCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(AddGuestCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<Result>($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<Result>($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure<Result>("User is not a group member");
        }

        if (group.Guests.Any(x => x.Name == command.GuestName))
        {
            return Result.Failure<Result>($"Guest with name {command.GuestName} already exists");
        }

        var now = DateTime.UtcNow;

        var newGuest = new Guest
        {
            Id = Guid.NewGuid().ToString(),
            Name = command.GuestName,
            Joined = now
        };

        var updatedGroup = group with
        {
            Guests = group.Guests.Concat([newGuest]).ToList(),
            Updated = now
        };

        var updateResult = await _groupsRepository.Update(updatedGroup, ct);

        if (updateResult.IsFailure)
        {
            return updateResult.ConvertFailure<Result>();
        }

        return Result.Success();
    }
}