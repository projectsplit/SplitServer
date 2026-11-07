using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Extensions;

public static class GroupsRepositoryExtensions
{
    public const string ChangedMeanwhileError = "The group changed while this was being saved. Please try again";

    /// <summary>
    /// Saves a group that was changed in one step, refusing if someone else wrote to it in between.
    /// The refusal is for the user: whatever they were doing is quick to redo against the group as
    /// it now is, and that beats working out here which of two concurrent edits to keep.
    /// </summary>
    public static async Task<Result> UpdateIfUnchangedOrFail(
        this IGroupsRepository groupsRepository,
        Group group,
        DateTime expectedUpdated,
        CancellationToken ct)
    {
        var result = await groupsRepository.UpdateIfUnchanged(group, expectedUpdated, ct);

        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        return result.Value ? Result.Success() : Result.Failure(ChangedMeanwhileError);
    }
}
