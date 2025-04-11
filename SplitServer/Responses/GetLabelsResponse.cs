using SplitServer.Requests;

namespace SplitServer.Responses;

public class GetLabelsResponse
{
    public required List<LabelResponseItem> Labels { get; init; }
}