using System.Text;

namespace SplitServer.Services;

public class JoinService
{
    private static readonly char[] NonAmbiguousChars = "abcdefghjkmnprstuvwxyz".ToCharArray();

    public static string GenerateToken(int length)
    {
        var builder = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            var randomIndex = Random.Shared.Next(NonAmbiguousChars.Length);

            builder.Append(NonAmbiguousChars[randomIndex]);
        }

        return builder.ToString();
    }
}