using System.Data;
using Microsoft.Data.SqlClient;

using GharMitra.Application.Common;

namespace GharMitra.Infrastructure.Data;

/// <summary>
/// Turns the authenticated caller into the SQL filter every query must carry.
///
/// Services call <see cref="For"/> instead of writing their own WHERE clause, so
/// omitting the tenant filter takes deliberate effort rather than a moment of
/// forgetfulness. Mandatory rules 4 and 5.
/// </summary>
public sealed class TenantScope
{
    private TenantScope(int familyId, int? familyMemberId)
    {
        FamilyId = familyId;
        FamilyMemberId = familyMemberId;
    }

    public int FamilyId { get; }

    /// <summary>
    /// Null when the caller may see the whole family. Set when the caller is
    /// restricted to their own rows.
    /// </summary>
    public int? FamilyMemberId { get; }

    public bool IsWholeFamily => FamilyMemberId is null;

    /// <summary>
    /// Builds the scope for a read, given the permission that grants family-wide
    /// visibility. A caller without it is narrowed to their own rows.
    /// </summary>
    public static TenantScope For(ICurrentUser user, string familyWidePermission)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required.");

        return user.Has(familyWidePermission)
            ? new TenantScope(user.FamilyId, null)
            : new TenantScope(user.FamilyId, user.FamilyMemberId);
    }

    /// <summary>Scope for a write: always the caller's own family and member.</summary>
    public static TenantScope Own(ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required.");

        return new TenantScope(user.FamilyId, user.FamilyMemberId);
    }

    /// <summary>
    /// The predicate to append to a WHERE clause, e.g.
    /// <c>e.FamilyId = @FamilyId AND e.FamilyMemberId = @ScopeMemberId</c>.
    /// </summary>
    public string Predicate(string alias)
    {
        var prefix = string.IsNullOrEmpty(alias) ? string.Empty : alias + ".";

        return IsWholeFamily
            ? $"{prefix}FamilyId = @FamilyId"
            : $"{prefix}FamilyId = @FamilyId AND {prefix}FamilyMemberId = @ScopeMemberId";
    }

    /// <summary>Binds the parameters the predicate refers to.</summary>
    public void AddParameters(SqlCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Parameters.Add("@FamilyId", SqlDbType.Int).Value = FamilyId;

        if (!IsWholeFamily)
            command.Parameters.Add("@ScopeMemberId", SqlDbType.Int).Value = FamilyMemberId!.Value;
    }
}
