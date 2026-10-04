using GharMitra.Application.Incomes.DTOs;

namespace GharMitra.Application.Incomes.Interfaces;

public interface IIncomeService
{
    Task<IReadOnlyList<IncomeDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IncomeDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomeDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<IncomeDto> CreateAsync(
        CreateIncomeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid id,
        UpdateIncomeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}