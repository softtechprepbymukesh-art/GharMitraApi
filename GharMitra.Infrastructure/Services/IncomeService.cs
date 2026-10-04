using GharMitra.Application.Incomes.DTOs;
using GharMitra.Application.Incomes.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Services;

public sealed class IncomeService : IIncomeService
{
    private readonly GharMitraDbContext _context;

    public IncomeService(GharMitraDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<IncomeDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var incomes = await _context.Incomes
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IncomeDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return incomes.Select(MapToDto).ToList();
    }

    public async Task<IncomeDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Incomes
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.Id == id)
            .Select(x => MapToDto(x))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Incomes
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.CategoryId == categoryId && x.IsActive)
            .OrderByDescending(x => x.IncomeDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => MapToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IncomeDto> CreateAsync(
        CreateIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        // Verify category exists
        var categoryExists = await _context.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new ArgumentException($"Category with ID {request.CategoryId} not found.", nameof(request.CategoryId));

        var income = new Income(
            request.SourceName,
            request.Amount,
            request.IncomeDate,
            request.CategoryId);

        _context.Incomes.Add(income);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with category
        await _context.Entry(income).Reference(x => x.Category).LoadAsync(cancellationToken);

        return MapToDto(income);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdateRequest(request);

        var categoryExists = await _context.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new ArgumentException($"Category with ID {request.CategoryId} not found.", nameof(request.CategoryId));

        var income = await _context.Incomes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (income is null)
            return false;

        income.Update(
            request.SourceName,
            request.Amount,
            request.IncomeDate,
            request.CategoryId);

        _context.Incomes.Update(income);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var income = await _context.Incomes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (income is null)
            return false;

        _context.Incomes.Remove(income);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var income = await _context.Incomes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (income is null)
            return false;

        income.Deactivate();
        _context.Incomes.Update(income);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ActivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var income = await _context.Incomes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (income is null)
            return false;

        income.Activate();
        _context.Incomes.Update(income);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static IncomeDto MapToDto(Income income)
    {
        return new IncomeDto(
            income.Id,
            income.SourceName,
            income.Amount,
            income.IncomeDate,
            income.CategoryId,
            income.Category?.Name,
            income.IsActive,
            income.CreatedAt,
            income.UpdatedAt);
    }

    private static void ValidateCreateRequest(CreateIncomeRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.SourceName))
            throw new ArgumentException("Source name is required.", nameof(request.SourceName));

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(request.Amount));

        if (request.IncomeDate > DateTime.UtcNow)
            throw new ArgumentException("Income date cannot be in the future.", nameof(request.IncomeDate));

        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.", nameof(request.CategoryId));
    }

    private static void ValidateUpdateRequest(UpdateIncomeRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.SourceName))
            throw new ArgumentException("Source name is required.", nameof(request.SourceName));

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(request.Amount));

        if (request.IncomeDate > DateTime.UtcNow)
            throw new ArgumentException("Income date cannot be in the future.", nameof(request.IncomeDate));

        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.", nameof(request.CategoryId));
    }
}