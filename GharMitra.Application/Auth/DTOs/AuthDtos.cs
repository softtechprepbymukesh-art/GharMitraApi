using System.ComponentModel.DataAnnotations;

namespace GharMitra.Application.Auth.DTOs;

/// <summary>
/// Stage 0 onboarding: creates the family, the first member and the Family Head
/// login in one transaction.
/// </summary>
public sealed class RegisterFamilyRequest
{
    [Required, MaxLength(150)]
    public string FamilyName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string MemberName { get; set; } = string.Empty;

    /// <summary>Relation of the registering person, e.g. Self, Father.</summary>
    [Required, MaxLength(50)]
    public string Relation { get; set; } = "Self";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    /// <summary>IANA time zone, e.g. Asia/Kolkata. Drives reminders and date bucketing.</summary>
    [MaxLength(64)]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}

/// <summary>Adds a member who can log in, under the caller's family.</summary>
public sealed class InviteMemberRequest
{
    [Required, MaxLength(150)]
    public string MemberName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Relation { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Adult, Child or Viewer. FamilyHead cannot be granted here.</summary>
    [Required, MaxLength(50)]
    public string RoleCode { get; set; } = "Adult";
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    int FamilyId,
    int FamilyMemberId,
    string Email,
    string RoleCode,
    IReadOnlyList<string> Permissions);
