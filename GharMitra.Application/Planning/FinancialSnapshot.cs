namespace GharMitra.Application.Planning;

/// <summary>
/// The raw monthly facts read from the database. Nothing here is derived, and
/// nothing here is stored: this is the input to <see cref="SafeToSpendCalculator"/>.
///
/// Keeping the facts and the arithmetic apart is what makes Stage 3 testable
/// without a database, and is why mandatory rules 6 and 7 hold - no calculated
/// value is ever persisted.
/// </summary>
public sealed record FinancialSnapshot
{
    /// <summary>Income actually recorded for the month.</summary>
    public decimal Income { get; init; }

    /// <summary>Expenses already recorded, excluding loan and investment rows.</summary>
    public decimal SpentSoFar { get; init; }

    /// <summary>Investment contributions made this month (SIP, LIC, PPF...).</summary>
    public decimal Investments { get; init; }

    /// <summary>Loan payments made this month: EMI plus any extra principal.</summary>
    public decimal LoanPayments { get; init; }

    /// <summary>
    /// Recurring bills that are due this month but not yet paid. Money that is
    /// already committed, so it is not available to spend.
    /// </summary>
    public decimal UpcomingRecurring { get; init; }

    /// <summary>
    /// This month's share of known future one-off costs, e.g. a 12,000 property
    /// tax due in 6 months contributes 2,000.
    /// </summary>
    public decimal ReservedForPlanned { get; init; }

    /// <summary>What the family intends to put toward goals this month.</summary>
    public decimal GoalContributions { get; init; }

    public static FinancialSnapshot Empty => new();
}

/// <summary>A single goal, reduced to what the planner needs.</summary>
public sealed record GoalSnapshot(
    string Name,
    decimal TargetAmount,
    decimal SavedAmount,
    DateOnly TargetDate,
    int Priority = 0);

/// <summary>A category budget and what has been spent against it.</summary>
public sealed record BudgetSnapshot(
    string CategoryName,
    decimal BudgetAmount,
    decimal SpentAmount);
