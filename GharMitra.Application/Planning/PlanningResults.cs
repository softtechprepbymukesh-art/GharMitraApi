namespace GharMitra.Application.Planning;

/// <summary>
/// The answer to the product's central question, with every deduction shown.
/// The breakdown matters as much as the number: a family that is told "you can
/// spend 15,771" without seeing why cannot act on it.
/// </summary>
public sealed record SafeToSpend(
    decimal Income,
    decimal SpentSoFar,
    decimal Investments,
    decimal LoanPayments,
    decimal UpcomingRecurring,
    decimal ReservedForPlanned,
    decimal GoalContributions)
{
    /// <summary>Everything already spent or already promised to something.</summary>
    public decimal TotalCommitted =>
        SpentSoFar + Investments + LoanPayments +
        UpcomingRecurring + ReservedForPlanned + GoalContributions;

    /// <summary>What is genuinely free to spend. Can be negative.</summary>
    public decimal Amount => Income - TotalCommitted;

    public bool IsOverCommitted => Amount < 0;

    /// <summary>
    /// Share of income kept rather than consumed. Investments, goal money and
    /// loan principal are saving, not spending - but this uses the simple
    /// definition the dashboard shows: income minus what left as expenses.
    /// </summary>
    public decimal SavingsRatePercent =>
        Income <= 0 ? 0 : Round((Income - SpentSoFar - UpcomingRecurring) / Income * 100m);

    public decimal ExpenseRatePercent =>
        Income <= 0 ? 0 : Round((SpentSoFar + UpcomingRecurring) / Income * 100m);

    internal static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Whether a goal can be reached in the time left.</summary>
public sealed record GoalFeasibility(
    string Name,
    decimal TargetAmount,
    decimal SavedAmount,
    decimal RemainingAmount,
    int MonthsRemaining,
    decimal RequiredMonthlySaving,
    decimal ProgressPercent,
    bool IsAchievable,
    string Explanation);

/// <summary>Budget consumption for one category.</summary>
public sealed record BudgetUtilization(
    string CategoryName,
    decimal BudgetAmount,
    decimal SpentAmount,
    decimal RemainingAmount,
    decimal UtilizationPercent,
    BudgetStatus Status);

public enum BudgetStatus
{
    /// <summary>Under 80% consumed.</summary>
    OnTrack = 0,

    /// <summary>80% or more consumed but not yet over.</summary>
    Warning = 1,

    /// <summary>Spending has passed the budget.</summary>
    Exceeded = 2
}

/// <summary>
/// The answer to "can I afford this?", including what it would cost the family
/// if they went ahead anyway.
/// </summary>
public sealed record AffordabilityVerdict(
    decimal RequestedAmount,
    decimal SafeToSpend,
    bool CanAfford,
    decimal Shortfall,
    IReadOnlyList<string> Options);
