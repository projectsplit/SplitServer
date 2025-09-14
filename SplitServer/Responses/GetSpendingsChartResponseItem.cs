namespace SplitServer.Responses;

public class GetSpendingsChartResponseItem
{
    public required decimal ShareAmount { get; init; }
    public required decimal AccumulativeShareAmount { get; init; }
    public required decimal PaymentAmount { get; init; }
    public required decimal AccumulativePaymentAmount { get; init; }
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
}