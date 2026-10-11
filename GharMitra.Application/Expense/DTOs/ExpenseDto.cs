namespace GharMitra.Application.Expenses.DTOs;

public sealed record ExpenseDto(
    int Id,
    int FamilyId,
    int FamilyMemberId,
    Guid CategoryId,
    decimal Amount,
    DateTime ExpenseDate,
    string ExpenseType,
    string PaymentMethod,
    string SpendingType,
    bool IsEssential,
    bool IsRecurring,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CategoryName);