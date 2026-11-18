using System.Globalization;

namespace SplitServer.Services.Plus;

/// <summary>What a pass should do about one account's notices that its free Plus days are ending.</summary>
public enum PlusTrialReminderAction
{
    /// <summary>Nothing is owed right now. Leave the account as it is.</summary>
    None = 0,

    /// <summary>Send the first notice: the free days end in about a week.</summary>
    SendFirst,

    /// <summary>Send the last notice: the free days end within a day.</summary>
    SendFinal,

    /// <summary>
    /// Nothing will ever be owed — the free days are over, or the account has Plus another way —
    /// so record that, without sending anything, and stop looking at it.
    /// </summary>
    MarkDone,
}

/// <summary>
/// The notices that an account's free Plus days are about to end: when one is due, and what it says.
///
/// Two at most, a week out and a day out, and they exist so that the day the Plus features lock is
/// never the first the person hears of it. Each says plainly what stops being free and that
/// everything else does not, because the fear a notice like this raises is that the whole app is
/// about to cost money, and it is not.
/// </summary>
public static class PlusTrialReminder
{
    /// <summary>Opens the Plus sheet in the app, where the plans are.</summary>
    public const string Url = "/?plus=1";

    /// <summary>The stage an account is at once the first notice has gone out.</summary>
    public const int FirstStage = 1;

    /// <summary>The stage an account is at once there is nothing more to send it.</summary>
    public const int DoneStage = 2;

    /// <summary>
    /// What ending means, in both notices. Names the Plus features one by one rather than saying
    /// "some features", and says in the same breath that the rest is unaffected.
    /// </summary>
    private const string WhatChanges =
        "After that, budgeting, analytics, search, places and payments from notifications need a subscription. " +
        "The rest of Buqs stays free.";

    /// <summary>
    /// What to do for an account whose access is <paramref name="access"/> and which has reached
    /// <paramref name="stage"/> of the notices.
    /// </summary>
    /// <remarks>
    /// The last notice takes precedence over the first. An account that comes into view already
    /// inside the final day — the worker was down, or the reminder settings changed — gets the one
    /// notice that is still true, not two in the same minute. Never anything for an account that
    /// subscribed or has Plus for life: its free days ending changes nothing for it.
    /// </remarks>
    public static PlusTrialReminderAction Decide(
        PlusAccess access,
        int stage,
        DateTime now,
        int reminderDays,
        int finalReminderDays)
    {
        if (stage >= DoneStage)
        {
            return PlusTrialReminderAction.None;
        }

        switch (access.Kind)
        {
            // Plus is not live, so no free days are running and none can be ending.
            case PlusAccessKind.Open:
                return PlusTrialReminderAction.None;

            case PlusAccessKind.Subscription:
            case PlusAccessKind.Lifetime:
            case PlusAccessKind.None:
                return PlusTrialReminderAction.MarkDone;
        }

        if (access.TrialEndsAt is not { } trialEndsAt)
        {
            return PlusTrialReminderAction.None;
        }

        var left = trialEndsAt - now;

        if (finalReminderDays > 0 && left <= TimeSpan.FromDays(finalReminderDays))
        {
            return PlusTrialReminderAction.SendFinal;
        }

        if (reminderDays > 0 && left <= TimeSpan.FromDays(reminderDays) && stage < FirstStage)
        {
            return PlusTrialReminderAction.SendFirst;
        }

        return PlusTrialReminderAction.None;
    }

    /// <summary>The title of the first notice, with the end date as the reader's own calendar has it.</summary>
    public static string FirstTitle(DateTime trialEndsAtUtc, string? timeZoneId) =>
        $"Your free Plus days end on {DayAndMonth(trialEndsAtUtc, timeZoneId)}";

    public static string FirstBody(DateTime trialEndsAtUtc, DateTime now)
    {
        var days = DaysLeft(trialEndsAtUtc, now);

        return $"You have {days} {(days == 1 ? "day" : "days")} left of Buqs Plus. {WhatChanges}";
    }

    /// <summary>
    /// The title of the last notice. "Tomorrow" when the end falls on the reader's next calendar
    /// day, which sent a day ahead it does; but a notice that goes out late must not promise a day
    /// more than there is, so it says "today" or names the date when that is what is true.
    /// </summary>
    public static string FinalTitle(DateTime trialEndsAtUtc, DateTime now, string? timeZoneId)
    {
        var endsOn = PlusDates.ToLocal(trialEndsAtUtc, timeZoneId).Date;
        var today = PlusDates.ToLocal(now, timeZoneId).Date;

        if (endsOn == today)
        {
            return "Your free Plus days end today";
        }

        return endsOn == today.AddDays(1)
            ? "Your free Plus days end tomorrow"
            : FirstTitle(trialEndsAtUtc, timeZoneId);
    }

    public static string FinalBody => WhatChanges;

    /// <summary>
    /// Whole days left, rounded up, and never less than one while any time is left at all: six days
    /// and twenty-three hours is "7 days" to the person reading, and the last few hours are "1 day",
    /// not none.
    /// </summary>
    public static int DaysLeft(DateTime trialEndsAtUtc, DateTime now) =>
        Math.Max(1, (int)Math.Ceiling((trialEndsAtUtc - now).TotalDays));

    private static string DayAndMonth(DateTime utc, string? timeZoneId) =>
        PlusDates.ToLocal(utc, timeZoneId).ToString("d MMMM", CultureInfo.InvariantCulture);
}
