using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Requests;

namespace SplitServer.Services;

public class NonGroupService
{
    private readonly IUsersRepository _usersRepository;

    public NonGroupService(IUsersRepository usersRepository)
    {
        _usersRepository = usersRepository;
    }

    public static List<Label> CreateLabelsWithIds(List<LabelRequestItem> labelItems, List<Label> userLabels)
    {
        return labelItems
            .Select(
                x =>
                    userLabels.SingleOrDefault(xx => xx.Text == x.Text) ??
                    new Label { Id = Guid.NewGuid().ToString(), Text = x.Text, Color = x.Color })
            .ToList();
    }

    public async Task<Result> AddLabelsToUserIfMissing(User user, List<Label> labels, DateTime now, CancellationToken ct)
    {
        var labelsNotInUser = labels.Where(x => !user.Labels.Select(xx => xx.Id).Contains(x.Id)).ToList();

        if (labelsNotInUser.Count <= 0)
        {
            return Result.Success();
        }

        return await _usersRepository.Update(
            user with { Labels = user.Labels.Concat(labelsNotInUser).DistinctBy(x => x.Id).ToList(), Updated = now },
            ct);
    }
}