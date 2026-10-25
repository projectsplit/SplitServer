namespace SplitServer.Requests;

public class DismissDonationPromptRequest
{
    /// <summary>
    /// True for "don't ask again", which is permanent. False is an ordinary "not now" and only starts
    /// the next cooldown.
    /// </summary>
    public required bool OptOut { get; init; }
}
