namespace GharMitra.Application.Common;

/// <summary>
/// The authenticated caller, resolved from JWT claims by the API layer.
///
/// This is the ONLY source of FamilyId in the system. Mandatory rules 2 and 3:
/// FamilyId must never be read from a request body or query string, because a
/// caller who controls it can read and write another family's finances.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    /// <summary>The tenant. Every business query filters on this first.</summary>
    int FamilyId { get; }

    /// <summary>Which person in the family is acting.</summary>
    int FamilyMemberId { get; }

    string RoleCode { get; }

    IReadOnlySet<string> Permissions { get; }

    bool Has(string permission);
}

/// <summary>
/// Permission codes, matching the Permissions table seeded by 04_Auth.sql.
/// </summary>
public static class Permission
{
    public const string FamilyManage = "family.manage";
    public const string FamilyView = "family.view";

    public const string ExpenseRecordOwn = "expense.record.own";
    public const string ExpenseViewOwn = "expense.view.own";
    public const string ExpenseViewFamily = "expense.view.family";

    public const string IncomeRecordOwn = "income.record.own";
    public const string IncomeViewOwn = "income.view.own";
    public const string IncomeViewFamily = "income.view.family";

    public const string AccountManage = "account.manage";
    public const string AccountViewFamily = "account.view.family";

    public const string CategoryManage = "category.manage";

    public const string BudgetManage = "budget.manage";
    public const string BudgetViewFamily = "budget.view.family";

    public const string GoalManage = "goal.manage";
    public const string GoalViewFamily = "goal.view.family";

    public const string LoanManage = "loan.manage";
    public const string LoanView = "loan.view";

    public const string InvestmentManage = "investment.manage";

    public const string ReportsViewOwn = "reports.view.own";
    public const string ReportsViewFamily = "reports.view.family";
}
