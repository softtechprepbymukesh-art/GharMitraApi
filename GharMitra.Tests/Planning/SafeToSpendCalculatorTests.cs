using GharMitra.Application.Planning;

namespace GharMitra.Tests.Planning;

public class SafeToSpendCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    /// <summary>
    /// The worked example in Docs/FUNCTIONAL_FLOW.md. If this number ever
    /// changes, the documentation is wrong or the rules have shifted.
    /// </summary>
    [Fact]
    public void WorkedExample_FromFunctionalFlowDoc()
    {
        var snapshot = new FinancialSnapshot
        {
            Income = 100_000m,
            SpentSoFar = 29_800m,        // fixed bills 6,800 + variable 23,000
            Investments = 15_000m,       // LIC 5,000 + SIP 10,000
            LoanPayments = 31_000m,      // home loan EMI
            ReservedForPlanned = 2_000m, // property tax 12,000 over 6 months
            GoalContributions = 6_429m   // vacation 90,000 over 14 months
        };

        var result = SafeToSpendCalculator.Calculate(snapshot);

        Assert.Equal(84_229m, result.TotalCommitted);
        Assert.Equal(15_771m, result.Amount);
        Assert.False(result.IsOverCommitted);
    }

    [Fact]
    public void UpcomingBills_ReduceWhatIsAvailable()
    {
        var withoutBills = SafeToSpendCalculator.Calculate(
            new FinancialSnapshot { Income = 50_000m, SpentSoFar = 10_000m });

        var withBills = SafeToSpendCalculator.Calculate(
            new FinancialSnapshot
            {
                Income = 50_000m, SpentSoFar = 10_000m, UpcomingRecurring = 8_000m
            });

        Assert.Equal(40_000m, withoutBills.Amount);
        Assert.Equal(32_000m, withBills.Amount);
    }

    [Fact]
    public void OverCommitted_IsReportedNotClampedToZero()
    {
        var result = SafeToSpendCalculator.Calculate(
            new FinancialSnapshot { Income = 50_000m, SpentSoFar = 60_000m });

        Assert.True(result.IsOverCommitted);
        Assert.Equal(-10_000m, result.Amount);
    }

    [Fact]
    public void ZeroIncome_DoesNotDivideByZero()
    {
        var result = SafeToSpendCalculator.Calculate(
            new FinancialSnapshot { SpentSoFar = 500m });

        Assert.Equal(0m, result.SavingsRatePercent);
        Assert.Equal(0m, result.ExpenseRatePercent);
    }

    [Fact]
    public void Rates_AreDerivedFromIncome()
    {
        var result = SafeToSpendCalculator.Calculate(
            new FinancialSnapshot { Income = 100_000m, SpentSoFar = 55_000m });

        Assert.Equal(45m, result.SavingsRatePercent);
        Assert.Equal(55m, result.ExpenseRatePercent);
    }
}

public class PlannedExpenseReserveTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    [Fact]
    public void SpreadsCostOverMonthsRemaining()
    {
        var monthly = SafeToSpendCalculator.ReserveForPlannedExpense(
            12_000m, new DateOnly(2027, 4, 25), Today);

        Assert.Equal(2_000m, monthly);
    }

    [Fact]
    public void CostDueThisMonth_MustBeReservedInFull()
    {
        var monthly = SafeToSpendCalculator.ReserveForPlannedExpense(
            12_000m, Today, Today);

        Assert.Equal(12_000m, monthly);
    }

    [Fact]
    public void OverdueCost_MustBeReservedInFull()
    {
        var monthly = SafeToSpendCalculator.ReserveForPlannedExpense(
            5_000m, new DateOnly(2026, 8, 1), Today);

        Assert.Equal(5_000m, monthly);
    }

    [Fact]
    public void ZeroOrNegativeAmount_ReservesNothing()
    {
        Assert.Equal(0m, SafeToSpendCalculator.ReserveForPlannedExpense(0m, new DateOnly(2027, 1, 1), Today));
        Assert.Equal(0m, SafeToSpendCalculator.ReserveForPlannedExpense(-5m, new DateOnly(2027, 1, 1), Today));
    }
}

public class GoalFeasibilityTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    [Fact]
    public void AchievableGoal_ReportsRequiredMonthlySaving()
    {
        // Vacation: 1,20,000 by Dec 2027, 30,000 saved.
        var goal = new GoalSnapshot("Vacation", 120_000m, 30_000m, new DateOnly(2027, 12, 25));

        var result = SafeToSpendCalculator.AssessGoal(goal, monthlySpareCapacity: 15_000m, Today);

        Assert.Equal(14, result.MonthsRemaining);
        Assert.Equal(90_000m, result.RemainingAmount);
        Assert.Equal(6_428.57m, result.RequiredMonthlySaving);
        Assert.Equal(25m, result.ProgressPercent);
        Assert.True(result.IsAchievable);
    }

    [Fact]
    public void UnaffordableGoal_SaysHowMuchIsMissingAndSuggestsADate()
    {
        var goal = new GoalSnapshot("Car", 600_000m, 0m, new DateOnly(2027, 4, 25));

        var result = SafeToSpendCalculator.AssessGoal(goal, monthlySpareCapacity: 10_000m, Today);

        Assert.False(result.IsAchievable);
        Assert.Equal(100_000m, result.RequiredMonthlySaving);
        Assert.Contains("90,000.00", result.Explanation);   // the monthly gap
        Assert.Contains("60 months", result.Explanation);   // a realistic alternative
    }

    [Fact]
    public void FullyFundedGoal_IsAchievableWithNothingLeftToSave()
    {
        var goal = new GoalSnapshot("Emergency fund", 100_000m, 100_000m, new DateOnly(2027, 1, 25));

        var result = SafeToSpendCalculator.AssessGoal(goal, 0m, Today);

        Assert.True(result.IsAchievable);
        Assert.Equal(0m, result.RequiredMonthlySaving);
        Assert.Equal(100m, result.ProgressPercent);
    }

    [Fact]
    public void OverfundedGoal_DoesNotReportMoreThan100Percent()
    {
        var goal = new GoalSnapshot("Trip", 50_000m, 75_000m, new DateOnly(2027, 1, 25));

        var result = SafeToSpendCalculator.AssessGoal(goal, 0m, Today);

        Assert.Equal(100m, result.ProgressPercent);
        Assert.Equal(0m, result.RemainingAmount);
    }

    [Fact]
    public void PassedTargetDate_IsNotAchievable()
    {
        var goal = new GoalSnapshot("Festival", 20_000m, 5_000m, new DateOnly(2026, 9, 1));

        var result = SafeToSpendCalculator.AssessGoal(goal, 50_000m, Today);

        Assert.False(result.IsAchievable);
        Assert.Contains("target date has passed", result.Explanation);
    }

    [Fact]
    public void ZeroTarget_IsRejected()
        => Assert.Throws<ArgumentException>(() => SafeToSpendCalculator.AssessGoal(
            new GoalSnapshot("Bad", 0m, 0m, new DateOnly(2027, 1, 1)), 100m, Today));
}

public class BudgetUtilizationTests
{
    [Theory]
    [InlineData(10_000, 7_500, 75, BudgetStatus.OnTrack)]
    [InlineData(10_000, 8_000, 80, BudgetStatus.Warning)]
    [InlineData(10_000, 10_000, 100, BudgetStatus.OnTrack + 1)]   // exactly at budget = Warning
    [InlineData(10_000, 12_000, 120, BudgetStatus.Exceeded)]
    public void StatusReflectsConsumption(
        decimal budget, decimal spent, decimal expectedPercent, BudgetStatus expectedStatus)
    {
        var result = SafeToSpendCalculator.AssessBudget(
            new BudgetSnapshot("Grocery", budget, spent));

        Assert.Equal(expectedPercent, result.UtilizationPercent);
        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public void RemainingCanGoNegative_SoOverspendIsVisible()
    {
        var result = SafeToSpendCalculator.AssessBudget(
            new BudgetSnapshot("Grocery", 10_000m, 12_500m));

        Assert.Equal(-2_500m, result.RemainingAmount);
    }

    [Fact]
    public void NoBudgetSet_DoesNotReportAMisleadingPercentage()
    {
        var result = SafeToSpendCalculator.AssessBudget(
            new BudgetSnapshot("Grocery", 0m, 4_000m));

        Assert.Equal(0m, result.UtilizationPercent);
        Assert.Equal(BudgetStatus.OnTrack, result.Status);
        Assert.Equal(4_000m, result.SpentAmount);
    }
}

public class AffordabilityTests
{
    private static SafeToSpend Spare(decimal amount) =>
        SafeToSpendCalculator.Calculate(new FinancialSnapshot { Income = amount });

    /// <summary>The 50,000 question from Docs/FUNCTIONAL_FLOW.md.</summary>
    [Fact]
    public void CannotAfford_ExplainsTheGapAndTheAlternatives()
    {
        var result = SafeToSpendCalculator.CanAfford(
            requestedAmount: 50_000m,
            safeToSpend: Spare(15_771m),
            monthlyGoalContributions: 6_429m,
            emergencyFund: 200_000m,
            monthlyEssentialSpend: 50_000m);

        Assert.False(result.CanAfford);
        Assert.Equal(34_229m, result.Shortfall);

        var text = string.Join(" | ", result.Options);
        Assert.Contains("4 months", text);        // save up instead
        Assert.Contains("6 month(s)", text);      // pause goals
        Assert.Contains("emergency fund", text);  // and what it costs
    }

    [Fact]
    public void CanAfford_ReportsWhatIsLeftOver()
    {
        var result = SafeToSpendCalculator.CanAfford(5_000m, Spare(15_771m));

        Assert.True(result.CanAfford);
        Assert.Equal(0m, result.Shortfall);
        Assert.Contains("10,771.00", result.Options[0]);
    }

    [Fact]
    public void ExactlyAffordable_CountsAsAffordable()
    {
        var result = SafeToSpendCalculator.CanAfford(15_771m, Spare(15_771m));

        Assert.True(result.CanAfford);
    }

    [Fact]
    public void EmergencyFundOption_IsOmittedWhenItCannotCoverTheGap()
    {
        var result = SafeToSpendCalculator.CanAfford(
            50_000m, Spare(10_000m), emergencyFund: 100m, monthlyEssentialSpend: 50_000m);

        Assert.DoesNotContain(result.Options, o => o.Contains("emergency fund"));
    }

    [Fact]
    public void NothingSpare_StillGivesAnAnswerWithoutDividingByZero()
    {
        var result = SafeToSpendCalculator.CanAfford(1_000m, Spare(0m));

        Assert.False(result.CanAfford);
        Assert.Equal(1_000m, result.Shortfall);
        Assert.DoesNotContain(result.Options, o => o.Contains("months and buy it outright"));
    }

    [Fact]
    public void NonPositiveRequest_IsRejected()
        => Assert.Throws<ArgumentException>(() => SafeToSpendCalculator.CanAfford(0m, Spare(100m)));
}

public class WholeMonthsBetweenTests
{
    [Theory]
    [InlineData("2026-10-25", "2027-12-25", 14)]
    [InlineData("2026-10-25", "2026-11-25", 1)]
    [InlineData("2026-10-25", "2026-11-24", 0)]   // not a full month yet
    [InlineData("2026-10-25", "2026-10-25", 0)]
    [InlineData("2026-10-25", "2026-01-01", 0)]   // past dates never go negative
    public void CountsWholeCalendarMonths(string from, string to, int expected)
        => Assert.Equal(
            expected,
            SafeToSpendCalculator.WholeMonthsBetween(DateOnly.Parse(from), DateOnly.Parse(to)));
}
