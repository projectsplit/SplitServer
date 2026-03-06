namespace SplitServer.Responses;

public class GetUserLabelsResponse
{
   public required List<GetUserLabelsResponseItem>  Labels { get; init; }
}