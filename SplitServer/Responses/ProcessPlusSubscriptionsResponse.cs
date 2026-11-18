namespace SplitServer.Responses;

public class ProcessPlusSubscriptionsResponse
{
    /// <summary>Subscriptions re-read from Google Play on this pass.</summary>
    public required int Refreshed { get; init; }

    /// <summary>Subscriptions that were due a re-read but could not be given one. They are tried again next pass.</summary>
    public required int Failed { get; init; }

    /// <summary>Renewal reminders sent on this pass.</summary>
    public required int Reminded { get; init; }

    /// <summary>Notices sent on this pass that an account's free Plus days are ending.</summary>
    public required int TrialReminded { get; init; }
}
