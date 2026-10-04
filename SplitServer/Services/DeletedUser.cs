namespace SplitServer.Services;

public static class DeletedUser
{
    /// <summary>
    /// What to show in place of the name of an account that no longer exists. The start of the id
    /// rather than all of it: enough to tell two deleted people apart in one list, without printing
    /// a whole GUID where a name belongs.
    /// </summary>
    public static string Username(string userId) => $"deleted-user-{userId[..Math.Min(8, userId.Length)]}";
}