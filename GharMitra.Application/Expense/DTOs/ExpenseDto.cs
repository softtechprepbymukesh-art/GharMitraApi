using GharMitra.Domain.Entities;

namespace GharMitra.Application.Expenses.DTOs;

public sealed record ExpenseDto(
    int Id,
    int FamilyId,
    int FamilyMemberId,
    Guid CategoryId,
    string? CategoryName,
    Guid? SubCategoryId,
    string? SubCategoryName,
    decimal Amount,
    DateTime ExpenseDate,
    NeedOrWant NeedOrWant,
    Guid PaymentMethodId,
    string? PaymentMethodName,
    Guid SpendingTypeId,
    string? SpendingTypeName,
    bool IsEssential,
    bool IsRecurring,
    Guid? AccountId,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
