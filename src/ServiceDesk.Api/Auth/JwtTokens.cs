using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Contracts;

namespace ServiceDesk.Api.Auth;

public sealed class JwtTokens
{
    private const string Issuer = "ServiceDesk";
    private const string Audience = "ServiceDesk.Client";
    private const string RoleClaim = "role";
    private readonly SymmetricSecurityKey _key;

    public JwtTokens(string? encodedKey)
    {
        const string message = "Set Jwt:SigningKey to a Base64 key of at least 32 bytes.";
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encodedKey ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(message);
        }

        _key = new SymmetricSecurityKey(bytes);
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidateIssuer = true,
        ValidAudience = Audience,
        ValidateAudience = true,
        IssuerSigningKey = _key,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = RoleClaim
    };

    public AccessTokenResponse Issue(string userId, IList<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(30);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(RoleClaim, role)));

        var token = new JwtSecurityToken(
            Issuer, Audience, claims, now, expires,
            new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, roles);
    }
}
