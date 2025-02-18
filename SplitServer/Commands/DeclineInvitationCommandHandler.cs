using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeclineInvitationCommandHandler : IRequestHandler<DeclineInvitationCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public DeclineInvitationCommandHandler(
        IUsersRepository usersRepository,
        IInvitationsRepository invitationsRepository)
    {
        _usersRepository = usersRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result> Handle(DeclineInvitationCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var invitationMaybe = await _invitationsRepository.GetById(command.InvitationId, ct);

        if (invitationMaybe.HasNoValue)
        {
            return Result.Failure($"Invitation with id {command.InvitationId} was not found");
        }

        var invitation = invitationMaybe.Value;

        if (invitation.ReceiverId != command.UserId)
        {
            return Result.Failure("You cannot decline this invitation");
        }

        var deleteInvitationResult = await _invitationsRepository.Delete(command.InvitationId, ct);

        if (deleteInvitationResult.IsFailure)
        {
            return deleteInvitationResult.ConvertFailure<Result>();
        }

        return Result.Success();
    }
}