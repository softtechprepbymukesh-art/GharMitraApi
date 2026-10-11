using GharMitra.Application.Expenses.DTOs;

namespace GharMitra.Application.Expenses.Interfaces;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ExpenseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalExpensesByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<ExpenseDto> CreateAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        int id,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}