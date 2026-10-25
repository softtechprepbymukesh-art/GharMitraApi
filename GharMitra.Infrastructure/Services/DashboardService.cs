using System.Data;
using System.Globalization;
using GharMitra.Application.Common;
using GharMitra.Application.Planning;
using GharMitra.Application.Planning.DTOs;
using GharMitra.Application.Planning.Interfaces;
using GharMitra.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace GharMitra.Infrastructure.Services;

/// <summary>
/// Loads the month's recorded facts and hands them to
/// <see cref="SafeToSpendCalculator"/>. This class only reads and maps; all
/// arithmetic lives in the calculator so it stays unit-testable without SQL.
///
/// Month boundaries are computed in the family's own time zone, so an expense
/// recorded at 00:30 IST on the 1st counts in the right month.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    /// <summary>
    /// Expenses in these system categories are not day-to-day spending: they
    /// are wealth moving sideways, so they are reported on their own lines.
    /// </summary>
    private const string InvestmentCategory = "Investment";
    private const string LoanCategory = "Loan & EMI";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public DashboardService(ISqlConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        _currentUser = currentUser
            ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<DashboardDto> GetDashboardAsync(
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        var expenseScope = ExpenseScope();
        var incomeScope = IncomeScope();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var family = await LoadFamilyContextAsync(connection, _currentUser.FamilyId, cancellationToken);
        var (fromUtc, toUtc, resolvedYear, resolvedMonth) = ResolveMonth(family.TimeZoneId, year, month);

        var income = await SumIncomeAsync(connection, incomeScope, fromUtc, toUtc, cancellationToken);
        var categories = await LoadCategorySpendAsync(connection, expenseScope, fromUtc, toUtc, cancellationToken);

        var investments = categories
            .Where(c => c.Name.Equals(InvestmentCategory, StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Amount);

        var loanPayments = categories
            .Where(c => c.Name.Equals(LoanCategory, StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Amount);

        var dayToDay = categories
            .Where(c => !c.Name.Equals(InvestmentCategory, StringComparison.OrdinalIgnoreCase)
                     && !c.Name.Equals(LoanCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var snapshot = new FinancialSnapshot
        {
            Income = income,
            SpentSoFar = dayToDay.Sum(c => c.Amount),
            Investments = investments,
            LoanPayments = loanPayments

            // UpcomingRecurring, ReservedForPlanned and GoalContributions stay
            // zero until the Recurring, PlannedExpense and Goal modules exist.
            // The calculator already accounts for them, so wiring those modules
            // needs no change here beyond filling these three lines.
        };

        var money = SafeToSpendCalculator.Calculate(snapshot);
        var totalSpend = dayToDay.Sum(c => c.Amount);

        var topCategories = dayToDay
            .OrderByDescending(c => c.Amount)
            .Take(5)
            .Select(c => new CategorySpendDto(
                c.Name,
                c.Amount,
                totalSpend <= 0 ? 0m : Math.Round(c.Amount / totalSpend * 100m, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new DashboardDto(
            resolvedYear,
            resolvedMonth,
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(resolvedMonth),
            family.TimeZoneId,
            family.CurrencyCode,
            ToBreakdown(money),
            topCategories,
            BuildWarnings(money, income),
            BuildHeadline(money, income));
    }

    public async Task<AffordabilityDto> CanAffordAsync(
        AffordabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        var dashboard = await GetDashboardAsync(cancellationToken: cancellationToken);
        var m = dashboard.Money;

        var safeToSpend = SafeToSpendCalculator.Calculate(new FinancialSnapshot
        {
            Income = m.Income,
            SpentSoFar = m.SpentSoFar,
            Investments = m.Investments,
            LoanPayments = m.LoanPayments,
            UpcomingRecurring = m.UpcomingRecurring,
            ReservedForPlanned = m.ReservedForPlanned,
            GoalContributions = m.GoalContributions
        });

        var verdict = SafeToSpendCalculator.CanAfford(
            request.Amount,
            safeToSpend,
            monthlyGoalContributions: m.GoalContributions,
            emergencyFund: 0m,
            monthlyEssentialSpend: m.SpentSoFar);

        return new AffordabilityDto(
            verdict.RequestedAmount,
            request.Purpose,
            verdict.SafeToSpend,
            verdict.CanAfford,
            verdict.Shortfall,
            verdict.Options);
    }

    private static MoneyBreakdownDto ToBreakdown(SafeToSpend m) => new(
        m.Income, m.SpentSoFar, m.Investments, m.LoanPayments,
        m.UpcomingRecurring, m.ReservedForPlanned, m.GoalContributions,
        m.TotalCommitted, m.Amount, m.IsOverCommitted,
        m.SavingsRatePercent, m.ExpenseRatePercent);

    private static IReadOnlyList<string> BuildWarnings(SafeToSpend money, decimal income)
    {
        var warnings = new List<string>();

        if (income <= 0)
            warnings.Add("No income recorded for this month yet, so Safe-To-Spend cannot be trusted.");

        if (money.IsOverCommitted)
            warnings.Add($"Committed {Fmt(money.TotalCommitted)} against income of {Fmt(income)}. You are over by {Fmt(-money.Amount)}.");

        if (income > 0 && money.SavingsRatePercent < 10m && !money.IsOverCommitted)
            warnings.Add($"Savings rate is {money.SavingsRatePercent}%. Under 10% leaves little room for emergencies.");

        return warnings;
    }

    private static string BuildHeadline(SafeToSpend money, decimal income)
    {
        if (income <= 0)
            return "Record this month's income to see what is safe to spend.";

        return money.IsOverCommitted
            ? $"You are over-committed by {Fmt(-money.Amount)} this month."
            : $"You can safely spend {Fmt(money.Amount)} for the rest of this month.";
    }

    private static string Fmt(decimal amount)
        => amount.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Month start and end in the family's local time, converted to UTC for the
    /// query, because every timestamp is stored in UTC.
    /// </summary>
    private static (DateTime FromUtc, DateTime ToUtc, int Year, int Month) ResolveMonth(
        string timeZoneId, int? year, int? month)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        var y = year ?? localNow.Year;
        var m = month ?? localNow.Month;

        if (m is < 1 or > 12)
            throw new ArgumentException("Month must be between 1 and 12.", nameof(month));

        var localStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localStart.AddMonths(1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, tz),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, tz),
            y, m);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // A bad stored value must not take the dashboard down.
            return TimeZoneInfo.Utc;
        }
    }

    private TenantScope ExpenseScope()
    {
        if (!_currentUser.Has(Permission.ExpenseViewOwn) && !_currentUser.Has(Permission.ExpenseViewFamily))
            throw new UnauthorizedAccessException("You are not allowed to view the dashboard.");

        return TenantScope.For(_currentUser, Permission.ExpenseViewFamily);
    }

    private TenantScope IncomeScope()
        => TenantScope.For(_currentUser, Permission.IncomeViewFamily);

    private static async Task<(string TimeZoneId, string CurrencyCode)> LoadFamilyContextAsync(
        SqlConnection connection, int familyId, CancellationToken cancellationToken)
    {
        await using var command = connection
            .CreateCommand("SELECT TimeZoneId, CurrencyCode FROM Families WHERE Id = @Id")
            .With("@Id", SqlDbType.Int, familyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Family not found.");

        return (reader.GetString(0), reader.GetString(1).Trim());
    }

    private static async Task<decimal> SumIncomeAsync(
        SqlConnection connection, TenantScope scope,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(
            $"""
             SELECT COALESCE(SUM(Amount), 0) FROM Incomes
             WHERE {scope.Predicate(string.Empty)}
               AND IsActive = 1
               AND IncomeDate >= @From AND IncomeDate < @To
             """)
            .With("@From", SqlDbType.DateTime2, fromUtc)
            .With("@To", SqlDbType.DateTime2, toUtc);
        scope.AddParameters(command);

        return await command.ExecuteScalarAsync(cancellationToken) is decimal d ? d : 0m;
    }

    private static async Task<List<(string Name, decimal Amount)>> LoadCategorySpendAsync(
        SqlConnection connection, TenantScope scope,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = new List<(string, decimal)>();

        await using var command = connection.CreateCommand(
            $"""
             SELECT c.Name, SUM(e.Amount)
             FROM Expenses e
             INNER JOIN Categories c ON c.Id = e.CategoryId
             WHERE {scope.Predicate("e")}
               AND e.ExpenseDate >= @From AND e.ExpenseDate < @To
             GROUP BY c.Name
             """)
            .With("@From", SqlDbType.DateTime2, fromUtc)
            .With("@To", SqlDbType.DateTime2, toUtc);
        scope.AddParameters(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((reader.GetString(0), reader.GetDecimal(1)));

        return rows;
    }
}
