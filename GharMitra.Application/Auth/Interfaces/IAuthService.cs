using GharMitra.Application.Auth.DTOs;

namespace GharMitra.Application.Auth.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Stage 0: creates Family, first FamilyMember (Family Head) and User atomically.
    /// </summary>
    Task<AuthResponse> RegisterFamilyAsync(
        RegisterFamilyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a member with a login to the caller's own family.</summary>
    Task<AuthResponse> InviteMemberAsync(
        InviteMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);
}
