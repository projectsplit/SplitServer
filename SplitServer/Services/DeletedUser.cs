namespace SplitServer.Services;

public static class DeletedUser
{
    public static string Username(string userId) => $"deleted-user-{userId}";
}