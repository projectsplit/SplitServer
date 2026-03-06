using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetUserAndGroupsLabelsQueryHandler: IRequestHandler<GetUserAndGroupsLabelsQuery, Result<GetUserAndGroupsLabelsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;
    private readonly IGroupsRepository _groupsRepository;

    public GetUserAndGroupsLabelsQueryHandler(
        IUsersRepository usersRepository,
        IUserLabelsRepository userLabelsRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _userLabelsRepository = userLabelsRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetUserAndGroupsLabelsResponse>> Handle(GetUserAndGroupsLabelsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetUserAndGroupsLabelsResponse>($"User with id {query.UserId} was not found");
        }
        
        var user = userMaybe.Value;
        var userLabels = await _userLabelsRepository.GetByUserId(user.Id, ct);
        var groups = await _groupsRepository.GetAllByUserId(user.Id, ct);

        var labels = userLabels.Select(x => new GetUserAndGroupsLabelsResponseItem
        {
            Color = x.Color,
            Text = x.Text,
            Id = x.Id,
        }).Concat(groups.SelectMany(g => g.Labels).Select(x => new GetUserAndGroupsLabelsResponseItem
        {
            Color = x.Color,
            Text = x.Text,
            Id = x.Id,
        })).DistinctBy(x => x.Id).ToList();
        
        return new GetUserAndGroupsLabelsResponse
        {
            Labels = labels
        };
    }
}