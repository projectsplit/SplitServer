namespace SplitServer.Responses;

public class GetInactiveBudgetsInfoResponse
{
    public required List<GetInactiveBudgetsInfoResponseItem> Budgets { get; init; }
}