namespace GharMitra.Application.Incomes.DTOs;

public sealed record UpdateIncomeRequest(
    string SourceName,
    decimal Amount,
    DateTime IncomeDate,
    Guid CategoryId);