using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Requests;

namespace SplitServer.Services;

public class UserLabelService
{
    private readonly IUserLabelsRepository _userLabelsRepository;

    public UserLabelService(
        IUserLabelsRepository userLabelsRepository)
    {
        _userLabelsRepository = userLabelsRepository;
    }

    public async Task<Result> AddUserLabelsIfMissing(string userId, List<LabelRequestItem> labels, DateTime now, CancellationToken ct)
    {
        var userLabels = await _userLabelsRepository.GetByUserId(userId, ct);

        var missingUserLabels = labels.Where(l => !userLabels.Select(ul => ul.Text).Contains(l.Text)).ToList();

        if (missingUserLabels.Count <= 0)
        {
            return Result.Success();
        }

        var newUserLabels = missingUserLabels
            .Select(x => new UserLabel
            {
                Id = $"{userId}_{x.Text}",
                Created = now,
                Updated = now,
                UserId = userId,
                Text = x.Text,
                Color = x.Color
            })
            .ToList();

        return await _userLabelsRepository.InsertMany(newUserLabels, ct);
    }
}