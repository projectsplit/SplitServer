namespace SplitServer.Responses;

public class GetUserAndGroupsLabelsResponse
{
    public required List<GetUserAndGroupsLabelsResponseItem> Labels { get; init; }
}