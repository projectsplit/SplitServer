using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetUserInvitationsQueryHandler : IRequestHandler<GetUserInvitationsQuery, Result<GetUserInvitationsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public GetUserInvitationsQueryHandler(
        IUsersRepository usersRepository,
        IInvitationsRepository invitationsRepository)
    {
        _usersRepository = usersRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result<GetUserInvitationsResponse>> Handle(GetUserInvitationsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetUserInvitationsResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = Next.Parse<UserInvitationsNext>(query.Next);
        var maxCreatedDate = nextDetails?.MaxCreatedDate ?? DateTime.UtcNow;

        var invitations = await _invitationsRepository.GetByReceiverId(query.UserId, query.PageSize, maxCreatedDate, ct);

        return new GetUserInvitationsResponse
        {
            Invitations = invitations,
            Next = CreateNext(query.PageSize, invitations)
        };
    }

    private static string? CreateNext(int pageSize, List<Invitation> invitations)
    {
        return Next.Create(invitations, pageSize, x => new UserInvitationsNext { MaxCreatedDate = x.Last().Created });
    }
}

file class UserInvitationsNext
{
    public required DateTime MaxCreatedDate { get; init; }
}