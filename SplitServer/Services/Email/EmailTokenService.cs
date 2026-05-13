using System.Security.Cryptography;
using System.Text;

namespace SplitServer.Services.Email;

public class EmailTokenService
{
    private const int PasswordResetTokenByteLength = 32;
    private const int VerificationCodeDigits = 6;

    public string GeneratePasswordResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(PasswordResetTokenByteLength);
        return Base64UrlEncode(bytes);
    }

    public string GenerateVerificationCode()
    {
        var builder = new StringBuilder(VerificationCodeDigits);

        for (var i = 0; i < VerificationCodeDigits; i++)
        {
            builder.Append(RandomNumberGenerator.GetInt32(0, 10));
        }

        return builder.ToString();
    }

    public string Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
