using System.Security.Claims;
using GharMitra.Application.Common;
using GharMitra.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GharMitra.Api.Security;

/// <summary>
/// Reads the tenant identity out of the validated JWT. Because the token is
/// signed, these values cannot be forged by the caller — which is what makes
/// them safe to use as the family filter in every query.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private static readonly IReadOnlySet<string> NoPermissions =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly ClaimsPrincipal? _principal;
    private readonly Lazy<IReadOnlySet<string>> _permissions;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;

        _permissions = new Lazy<IReadOnlySet<string>>(() =>
            _principal is null
                ? NoPermissions
                : new HashSet<string>(
                    _principal.FindAll(GharMitraClaims.Permission).Select(c => c.Value),
                    StringComparer.Ordinal));
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Find(JwtRegisteredClaimNames.Sub) ?? Find(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token does not carry a valid user id.");

    public int FamilyId =>
        int.TryParse(Find(GharMitraClaims.FamilyId), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token does not carry a family id.");

    public int FamilyMemberId =>
        int.TryParse(Find(GharMitraClaims.FamilyMemberId), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token does not carry a family member id.");

    public string RoleCode => Find(GharMitraClaims.Role) ?? string.Empty;

    public IReadOnlySet<string> Permissions => _permissions.Value;

    public bool Has(string permission) => Permissions.Contains(permission);

    private string? Find(string claimType) => _principal?.FindFirst(claimType)?.Value;
}
