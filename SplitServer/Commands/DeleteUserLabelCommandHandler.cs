using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeleteUserLabelCommandHandler: IRequestHandler<DeleteUserLabelCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;
    private readonly IExpensesRepository _expensesRepository;


    public DeleteUserLabelCommandHandler(
        IUsersRepository usersRepository,
        IUserLabelsRepository userLabelsRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _userLabelsRepository = userLabelsRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result> Handle(DeleteUserLabelCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }
        var userLabelMaybe = await _userLabelsRepository.GetById(command.LabelId, ct);
        
        if (userLabelMaybe.HasNoValue)
        {
            return Result.Failure($"Label with id {command.LabelId} was not found");
        }

        var labelText = command.LabelId.Split('_').Last();
        if (await _expensesRepository.UserLabelInUse(labelText, ct))
        {
            return Result.Failure("Label is in use and cannot be deleted");
        }
        
        return await _userLabelsRepository.Delete(command.LabelId, ct);
    }
}