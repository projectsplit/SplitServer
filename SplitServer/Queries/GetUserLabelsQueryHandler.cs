using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetUserLabelsQueryHandler : IRequestHandler<GetUserLabelsQuery, Result<GetUserLabelsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;

    public GetUserLabelsQueryHandler(
        IUsersRepository usersRepository,
        IUserLabelsRepository userLabelsRepository)
    {
        _usersRepository = usersRepository;
        _userLabelsRepository = userLabelsRepository;
    }

    public async Task<Result<GetUserLabelsResponse>> Handle(GetUserLabelsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetUserLabelsResponse>($"User with id {query.UserId} was not found");
        }

        var user = userMaybe.Value;
        var labels = await _userLabelsRepository.GetByUserId(user.Id, ct);

        return new GetUserLabelsResponse
        {
            Labels = labels
                .Select(x => new GetUserLabelsResponseItem
                {
                    Color = x.Color,
                    Text = x.Text,
                    Id = x.Id,
                    Count = labels.Count
                })
                .ToList()
        };
    }
}