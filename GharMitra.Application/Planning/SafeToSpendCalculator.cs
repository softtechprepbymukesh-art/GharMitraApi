namespace GharMitra.Application.Planning;

/// <summary>
/// Stage 3 of the functional flow. Every figure here is DERIVED from recorded
/// facts and none of it is written back to the database (mandatory rule 6).
///
/// These are pure static functions on purpose: no connection, no clock, no
/// configuration. That makes the arithmetic the family's money depends on
/// fully testable, and keeps the rules in one readable place instead of
/// scattered through SQL.
/// </summary>
public static class SafeToSpendCalculator
{
    private const decimal BudgetWarningThresholdPercent = 80m;

    /// <summary>
    /// Income − spent − investments − loan payments − upcoming bills
    /// − reserved for planned costs − goal contributions.
    ///
    /// The last three lines are what separate a planner from a tracker: money
    /// promised to a future bill or a goal is not money available today.
    /// </summary>
    public static SafeToSpend Calculate(FinancialSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SafeToSpend(
            Income: snapshot.Income,
            SpentSoFar: snapshot.SpentSoFar,
            Investments: snapshot.Investments,
            LoanPayments: snapshot.LoanPayments,
            UpcomingRecurring: snapshot.UpcomingRecurring,
            ReservedForPlanned: snapshot.ReservedForPlanned,
            GoalContributions: snapshot.GoalContributions);
    }

    /// <summary>
    /// Monthly amount to set aside so a one-off future cost does not arrive as
    /// a shock. A cost already due or overdue must be reserved in full now.
    /// </summary>
    public static decimal ReserveForPlannedExpense(
        decimal amount,
        DateOnly dueDate,
        DateOnly today)
    {
        if (amount <= 0)
            return 0m;

        var months = WholeMonthsBetween(today, dueDate);

        return months <= 0
            ? amount
            : SafeToSpend.Round(amount / months);
    }

    /// <summary>
    /// Whether a goal is reachable, and what it needs per month. Compared
    /// against what is actually spare, so the answer reflects this family
    /// rather than a generic rule of thumb.
    /// </summary>
    public static GoalFeasibility AssessGoal(
        GoalSnapshot goal,
        decimal monthlySpareCapacity,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (goal.TargetAmount <= 0)
            throw new ArgumentException("Goal target must be greater than zero.", nameof(goal));

        var saved = Math.Max(0m, goal.SavedAmount);
        var remaining = Math.Max(0m, goal.TargetAmount - saved);
        var progress = SafeToSpend.Round(Math.Min(100m, saved / goal.TargetAmount * 100m));
        var months = WholeMonthsBetween(today, goal.TargetDate);

        if (remaining == 0m)
        {
            return new GoalFeasibility(
                goal.Name, goal.TargetAmount, saved, 0m, months, 0m, 100m, true,
                "Already funded.");
        }

        if (months <= 0)
        {
            return new GoalFeasibility(
                goal.Name, goal.TargetAmount, saved, remaining, 0, remaining, progress, false,
                $"The target date has passed with {Money(remaining)} still to save. Pick a new date.");
        }

        var required = SafeToSpend.Round(remaining / months);
        var achievable = required <= monthlySpareCapacity;

        var explanation = achievable
            ? $"Saving {Money(required)} a month for {months} months reaches this goal."
            : $"Needs {Money(required)} a month but only {Money(monthlySpareCapacity)} is spare. "
              + $"Either free up {Money(required - monthlySpareCapacity)} a month, or move the date "
              + $"to about {MonthsNeeded(remaining, monthlySpareCapacity)} months out.";

        return new GoalFeasibility(
            goal.Name, goal.TargetAmount, saved, remaining, months, required, progress,
            achievable, explanation);
    }

    public static BudgetUtilization AssessBudget(BudgetSnapshot budget)
    {
        ArgumentNullException.ThrowIfNull(budget);

        if (budget.BudgetAmount <= 0)
        {
            // No budget set: report the spend without a misleading percentage.
            return new BudgetUtilization(
                budget.CategoryName, 0m, budget.SpentAmount, 0m, 0m, BudgetStatus.OnTrack);
        }

        var percent = SafeToSpend.Round(budget.SpentAmount / budget.BudgetAmount * 100m);
        var remaining = SafeToSpend.Round(budget.BudgetAmount - budget.SpentAmount);

        var status = percent switch
        {
            > 100m => BudgetStatus.Exceeded,
            >= BudgetWarningThresholdPercent => BudgetStatus.Warning,
            _ => BudgetStatus.OnTrack
        };

        return new BudgetUtilization(
            budget.CategoryName, budget.BudgetAmount, budget.SpentAmount,
            remaining, percent, status);
    }

    /// <summary>
    /// Answers "can I afford X this month?" and, when the answer is no, what
    /// the family could change. Requirement 19 asks for the reasoning, not just
    /// a yes or no.
    /// </summary>
    public static AffordabilityVerdict CanAfford(
        decimal requestedAmount,
        SafeToSpend safeToSpend,
        decimal monthlyGoalContributions = 0m,
        decimal emergencyFund = 0m,
        decimal monthlyEssentialSpend = 0m)
    {
        ArgumentNullException.ThrowIfNull(safeToSpend);

        if (requestedAmount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(requestedAmount));

        var spare = safeToSpend.Amount;

        if (requestedAmount <= spare)
        {
            return new AffordabilityVerdict(
                requestedAmount, spare, true, 0m,
                [$"Leaves {Money(spare - requestedAmount)} spare this month."]);
        }

        var shortfall = SafeToSpend.Round(requestedAmount - spare);
        var options = new List<string>
        {
            $"Short by {Money(shortfall)}. Safe to spend this month is {Money(spare)}."
        };

        if (spare > 0)
        {
            var months = (int)Math.Ceiling(requestedAmount / spare);
            options.Add($"Save the spare amount for {months} months and buy it outright.");
        }

        if (monthlyGoalContributions > 0)
        {
            var monthsPaused = (int)Math.Ceiling(shortfall / monthlyGoalContributions);
            options.Add(
                $"Pause goal contributions for {monthsPaused} month(s), which delays those goals by the same time.");
        }

        if (emergencyFund >= shortfall && monthlyEssentialSpend > 0)
        {
            var before = SafeToSpend.Round(emergencyFund / monthlyEssentialSpend);
            var after = SafeToSpend.Round((emergencyFund - shortfall) / monthlyEssentialSpend);
            options.Add(
                $"Use the emergency fund, but cover drops from {before} to {after} months of expenses.");
        }

        return new AffordabilityVerdict(requestedAmount, spare, false, shortfall, options);
    }

    /// <summary>
    /// Whole months from <paramref name="from"/> to <paramref name="to"/>,
    /// never negative. Counts calendar months so a goal dated 1 Dec from
    /// 25 Oct gives 1, not 2.
    /// </summary>
    public static int WholeMonthsBetween(DateOnly from, DateOnly to)
    {
        if (to <= from)
            return 0;

        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;

        if (to.Day < from.Day)
            months--;

        return Math.Max(0, months);
    }

    private static int MonthsNeeded(decimal remaining, decimal perMonth)
        => perMonth <= 0 ? 0 : (int)Math.Ceiling(remaining / perMonth);

    private static string Money(decimal amount)
        => amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
}
