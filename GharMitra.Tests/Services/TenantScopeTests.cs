using GharMitra.Application.Common;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Tests.Services;

/// <summary>
/// TenantScope is the single place that decides which rows a caller may touch.
/// If these tests pass, mandatory rules 1-5 hold for every service that uses it.
/// </summary>
public class TenantScopeTests
{
    [Fact]
    public void FamilyHead_SeesWholeFamily()
    {
        var scope = TenantScope.For(FakeCurrentUser.Head(familyId: 7), Permission.ExpenseViewFamily);

        Assert.True(scope.IsWholeFamily);
        Assert.Equal(7, scope.FamilyId);
        Assert.Equal("e.FamilyId = @FamilyId", scope.Predicate("e"));
    }

    [Fact]
    public void AdultMember_IsNarrowedToOwnRows()
    {
        var scope = TenantScope.For(
            FakeCurrentUser.Adult(familyId: 7, memberId: 42), Permission.ExpenseViewFamily);

        Assert.False(scope.IsWholeFamily);
        Assert.Equal(42, scope.FamilyMemberId);
        Assert.Equal(
            "e.FamilyId = @FamilyId AND e.FamilyMemberId = @ScopeMemberId",
            scope.Predicate("e"));
    }

    [Fact]
    public void Child_NeverSeesFamilyWideIncome()
    {
        // Mandatory rule 9: a child must not reach family financial data.
        var scope = TenantScope.For(FakeCurrentUser.Child(), Permission.IncomeViewFamily);

        Assert.False(scope.IsWholeFamily);
    }

    [Fact]
    public void Predicate_AlwaysFiltersByFamilyFirst()
    {
        foreach (var user in new[]
                 {
                     FakeCurrentUser.Head(), FakeCurrentUser.Adult(), FakeCurrentUser.Child()
                 })
        {
            var scope = TenantScope.For(user, Permission.ExpenseViewFamily);

            Assert.StartsWith("FamilyId = @FamilyId", scope.Predicate(string.Empty));
        }
    }

    [Fact]
    public void AnonymousCaller_IsRejected()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => TenantScope.For(FakeCurrentUser.Anonymous(), Permission.ExpenseViewFamily));

        Assert.Throws<UnauthorizedAccessException>(
            () => TenantScope.Own(FakeCurrentUser.Anonymous()));
    }

    [Fact]
    public void Own_IsAlwaysTheCallersOwnMember_EvenForTheHead()
    {
        var scope = TenantScope.Own(FakeCurrentUser.Head(familyId: 3, memberId: 9));

        Assert.False(scope.IsWholeFamily);
        Assert.Equal(3, scope.FamilyId);
        Assert.Equal(9, scope.FamilyMemberId);
    }

    [Fact]
    public void AddParameters_BindsExactlyWhatThePredicateReferences()
    {
        using var command = new SqlCommand();
        TenantScope.For(FakeCurrentUser.Head(familyId: 5), Permission.ExpenseViewFamily)
            .AddParameters(command);

        Assert.Equal(5, command.Parameters["@FamilyId"].Value);
        Assert.False(command.Parameters.Contains("@ScopeMemberId"));

        using var scoped = new SqlCommand();
        TenantScope.For(FakeCurrentUser.Adult(familyId: 5, memberId: 8), Permission.ExpenseViewFamily)
            .AddParameters(scoped);

        Assert.Equal(5, scoped.Parameters["@FamilyId"].Value);
        Assert.Equal(8, scoped.Parameters["@ScopeMemberId"].Value);
    }

    [Fact]
    public void TwoFamilies_NeverProduceTheSameFilter()
    {
        using var a = new SqlCommand();
        using var b = new SqlCommand();

        TenantScope.For(FakeCurrentUser.Head(familyId: 1), Permission.ExpenseViewFamily)
            .AddParameters(a);
        TenantScope.For(FakeCurrentUser.Head(familyId: 2), Permission.ExpenseViewFamily)
            .AddParameters(b);

        Assert.NotEqual(a.Parameters["@FamilyId"].Value, b.Parameters["@FamilyId"].Value);
    }
}
