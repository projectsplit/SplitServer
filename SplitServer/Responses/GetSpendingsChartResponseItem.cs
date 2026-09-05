namespace SplitServer.Responses;

public class GetSpendingsChartResponseItem
{
    public required decimal ShareAmount { get; init; }
    public required decimal AccumulativeShareAmount { get; init; }
    public required decimal PaymentAmount { get; init; }
    public required decimal AccumulativePaymentAmount { get; init; }

    /// <summary>
    /// How much the user fronted for others, classified per expense rather than per bucket: an
    /// expense the user overpaid on counts as lending even if another expense in the same bucket
    /// was covered for them. Netting the two first would understate both sides and would make the
    /// numbers depend on the chosen granularity.
    /// </summary>
    public required decimal LentAmount { get; init; }

    public required decimal AccumulativeLentAmount { get; init; }
    public required decimal BorrowedAmount { get; init; }
    public required decimal AccumulativeBorrowedAmount { get; init; }
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
}