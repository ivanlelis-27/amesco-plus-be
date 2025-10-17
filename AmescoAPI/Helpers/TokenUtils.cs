using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;

public static class TokenUtils
{
    public static string GenerateJwtToken(
    string userId, string email, string firstName, string lastName, string mobile, string memberId, IConfiguration config, string? sessionId = null)
    {
        var jwtSettings = config.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("firstName", firstName ?? ""),
            new Claim("lastName", lastName ?? ""),
            new Claim("mobile", mobile ?? ""),
            new Claim("memberId", memberId ?? "")
        };

        if (!string.IsNullOrEmpty(sessionId))
        {
            claims.Add(new Claim("sid", sessionId)); // session id claim
        }

        var keyString = jwtSettings["Key"] ?? "";
        Console.WriteLine("JWT KEY USED FOR SIGNING: >" + keyString + "<");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(Convert.ToDouble(jwtSettings["ExpireMinutes"] ?? "60")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateTokenUrlSafe(int size = 32)
    {
        var bytes = new byte[size];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string ComputeSha256Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
