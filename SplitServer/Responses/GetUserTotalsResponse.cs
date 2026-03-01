namespace SplitServer.Responses;

public class GetUserTotalsResponse  
{
    public required Dictionary<string, Dictionary<string, decimal>> TotalSpent { get; init; }
    public required Dictionary<string, decimal> ConvertedTotalSpent { get; init; }

}