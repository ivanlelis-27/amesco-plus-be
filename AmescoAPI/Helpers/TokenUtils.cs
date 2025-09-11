using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities; // for Base64UrlEncode

public static class TokenUtils
{
    public static string GenerateTokenUrlSafe(int size = 32)
    {
        var bytes = new byte[size];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes); // URL-safe base64
    }

    public static string ComputeSha256Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
