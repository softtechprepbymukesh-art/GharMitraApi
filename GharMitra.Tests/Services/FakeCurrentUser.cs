using GharMitra.Application.Common;

namespace GharMitra.Tests.Services;

/// <summary>
/// Stands in for an authenticated caller. Defaults to a Family Head with every
/// permission; the factory methods below build the restricted roles.
/// </summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public static readonly string[] AllPermissions =
    [
        Permission.FamilyManage, Permission.FamilyView,
        Permission.ExpenseRecordOwn, Permission.ExpenseViewOwn, Permission.ExpenseViewFamily,
        Permission.IncomeRecordOwn, Permission.IncomeViewOwn, Permission.IncomeViewFamily,
        Permission.AccountManage, Permission.AccountViewFamily,
        Permission.CategoryManage,
        Permission.BudgetManage, Permission.BudgetViewFamily,
        Permission.GoalManage, Permission.GoalViewFamily,
        Permission.LoanManage, Permission.LoanView,
        Permission.InvestmentManage,
        Permission.ReportsViewOwn, Permission.ReportsViewFamily
    ];

    public FakeCurrentUser(
        int familyId = 1,
        int familyMemberId = 1,
        string roleCode = "FamilyHead",
        IEnumerable<string>? permissions = null,
        bool isAuthenticated = true)
    {
        FamilyId = familyId;
        FamilyMemberId = familyMemberId;
        RoleCode = roleCode;
        IsAuthenticated = isAuthenticated;
        Permissions = new HashSet<string>(permissions ?? AllPermissions, StringComparer.Ordinal);
    }

    public bool IsAuthenticated { get; }

    public Guid UserId { get; } = Guid.NewGuid();

    public int FamilyId { get; }

    public int FamilyMemberId { get; }

    public string RoleCode { get; }

    public IReadOnlySet<string> Permissions { get; }

    public bool Has(string permission) => Permissions.Contains(permission);

    /// <summary>Family Head of the given family: sees everything in that family.</summary>
    public static FakeCurrentUser Head(int familyId = 1, int memberId = 1)
        => new(familyId, memberId);

    /// <summary>Adult member: own rows only, no family-wide visibility.</summary>
    public static FakeCurrentUser Adult(int familyId = 1, int memberId = 2)
        => new(familyId, memberId, "Adult",
        [
            Permission.FamilyView,
            Permission.ExpenseRecordOwn, Permission.ExpenseViewOwn,
            Permission.IncomeRecordOwn, Permission.IncomeViewOwn,
            Permission.GoalManage, Permission.ReportsViewOwn,
            Permission.BudgetViewFamily, Permission.LoanView
        ]);

    /// <summary>Child: own expenses only, exactly as seeded by 04_Auth.sql.</summary>
    public static FakeCurrentUser Child(int familyId = 1, int memberId = 3)
        => new(familyId, memberId, "Child",
        [
            Permission.ExpenseRecordOwn, Permission.ExpenseViewOwn
        ]);

    public static FakeCurrentUser Anonymous()
        => new(0, 0, string.Empty, [], isAuthenticated: false);
}
