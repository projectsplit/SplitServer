using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public DeleteGroupCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        IInvitationsRepository invitationsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }
        
        var user = userMaybe.Value;
        
        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }
        
        var group = groupMaybe.Value;

        if (group.OwnerId != user.Id)
        {
            return Result.Failure("This group does not belong to user");
        }
        
        var deleteResult = await _groupsRepository.SoftDelete(group.Id, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult.ConvertFailure<Result>();
        }
        
        var deleteInvitationsResult = await _expensesRepository.SoftDelete(group.Id, ct);
        
        return Result.Success();
    }
}