namespace GharMitra.Application.Incomes.DTOs;

public sealed record IncomeDto(
    Guid Id,
    string SourceName,
    decimal Amount,
    DateTime IncomeDate,
    Guid CategoryId,
    string? CategoryName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
