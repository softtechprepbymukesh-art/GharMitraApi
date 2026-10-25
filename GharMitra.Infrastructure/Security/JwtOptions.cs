namespace GharMitra.Infrastructure.Security;

/// <summary>
/// Bound from the "Jwt" configuration section. The signing key must come from
/// user-secrets or an environment variable, never from a committed file.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "GharMitra";

    public string Audience { get; set; } = "GharMitra.Client";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it with " +
                "'dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 characters>\"' " +
                "or the Jwt__SigningKey environment variable.");
        }

        // HMAC-SHA256 needs a key at least as long as the hash it produces.
        if (System.Text.Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes (32 ASCII characters).");
        }
    }
}
