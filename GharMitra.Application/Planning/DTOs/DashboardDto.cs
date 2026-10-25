namespace GharMitra.Application.Planning.DTOs;

/// <summary>
/// Stage 4 of the functional flow: what the family sees when they open the app.
/// Every figure is computed on request from recorded facts. Nothing here is
/// stored (mandatory rules 6 and 7).
/// </summary>
public sealed record DashboardDto(
    int Year,
    int Month,
    string MonthName,
    string TimeZoneId,
    string CurrencyCode,
    MoneyBreakdownDto Money,
    IReadOnlyList<CategorySpendDto> TopCategories,
    IReadOnlyList<string> Warnings,
    string Headline);

public sealed record MoneyBreakdownDto(
    decimal Income,
    decimal SpentSoFar,
    decimal Investments,
    decimal LoanPayments,
    decimal UpcomingRecurring,
    decimal ReservedForPlanned,
    decimal GoalContributions,
    decimal TotalCommitted,
    decimal SafeToSpend,
    bool IsOverCommitted,
    decimal SavingsRatePercent,
    decimal ExpenseRatePercent);

public sealed record CategorySpendDto(
    string CategoryName,
    decimal Amount,
    decimal ShareOfSpendPercent);

/// <summary>Stage 5: "can I afford this?"</summary>
public sealed class AffordabilityRequest
{
    public decimal Amount { get; set; }

    /// <summary>Optional note, echoed back so the caller can label the answer.</summary>
    public string? Purpose { get; set; }
}

public sealed record AffordabilityDto(
    decimal RequestedAmount,
    string? Purpose,
    decimal SafeToSpend,
    bool CanAfford,
    decimal Shortfall,
    IReadOnlyList<string> Options);
