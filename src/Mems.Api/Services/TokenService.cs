/*
 * Project: Municipal Elections Companion API (MEMS)
 * Course: COMP4977
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Issues signed JWT bearer tokens for authenticated users.
 */
using System.Security.Claims;
using System.Text;
using Mems.Api.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mems.Api.Services;

/// <summary>JWT settings bound from the "Jwt" configuration section (override with Jwt__Key etc. environment variables).</summary>
public class JwtOptions
{
    public const string Section = "Jwt";
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 10080;

    public SymmetricSecurityKey SigningKey()
    {
        // HMAC-SHA256 requires at least 256 bits of key material.
        if (Encoding.UTF8.GetByteCount(Key) < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
    }
}

public class TokenService(JwtOptions options, TimeProvider clock)
{
    public string CreateToken(ApplicationUser user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor {
            Issuer = options.Issuer, Audience = options.Audience, Subject = new ClaimsIdentity(claims),
            IssuedAt = now, NotBefore = now, Expires = now.AddMinutes(options.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(options.SigningKey(), SecurityAlgorithms.HmacSha256)
        });
    }
}
