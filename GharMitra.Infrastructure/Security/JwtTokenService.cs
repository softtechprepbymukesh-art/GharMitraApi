using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GharMitra.Infrastructure.Security;

/// <summary>
/// Claim names shared by the token issuer (Infrastructure) and the claim reader (Api).
/// </summary>
public static class GharMitraClaims
{
    public const string FamilyId = "family_id";
    public const string FamilyMemberId = "family_member_id";
    public const string Role = "role_code";
    public const string Permission = "perm";
}

public sealed record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>
/// Issues the access token carrying the tenant identity. Everything the services
/// need to scope a query (family, member, role, permissions) travels in the token,
/// so no request is ever trusted to supply it.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(JwtOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken CreateAccessToken(
        Guid userId,
        int familyId,
        int familyMemberId,
        string email,
        string roleCode,
        IEnumerable<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(GharMitraClaims.FamilyId, familyId.ToString()),
            new(GharMitraClaims.FamilyMemberId, familyMemberId.ToString()),
            new(GharMitraClaims.Role, roleCode)
        };

        claims.AddRange(permissions.Select(p => new Claim(GharMitraClaims.Permission, p)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new IssuedToken(token, expires);
    }

    /// <summary>
    /// Refresh tokens are opaque random values. Only their SHA-256 hash is stored,
    /// so a database leak does not hand an attacker usable sessions.
    /// </summary>
    public static string CreateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public static string HashRefreshToken(string refreshToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public DateTime RefreshTokenExpiry() => DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
}
