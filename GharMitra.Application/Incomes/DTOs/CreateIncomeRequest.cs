namespace GharMitra.Application.Incomes.DTOs;

public sealed record CreateIncomeRequest(
    string SourceName,
    decimal Amount,
    DateTime IncomeDate,
    Guid CategoryId);