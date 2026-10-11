using GharMitra.Application.Expenses.DTOs;
using GharMitra.Application.Expenses.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly GharMitraDbContext _context;

    public ExpenseService(GharMitraDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Read Operations

    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(x => x.Category)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return expenses
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ExpenseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        var expense = await _context.Expenses
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        return expense is null
            ? null
            : MapToDto(expense);
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetByCategoryIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
            return Array.Empty<ExpenseDto>();

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.CategoryId == categoryId)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return expenses
            .Select(MapToDto)
            .ToList();
    }

    public async Task<decimal> GetTotalExpensesByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
            return 0m;

        return await _context.Expenses
            .AsNoTracking()
            .Where(x => x.CategoryId == categoryId)
            .SumAsync(x => x.Amount, cancellationToken);
    }

    #endregion

    #region Create

    public async Task<ExpenseDto> CreateAsync(
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var category = await _context.Categories
            .FirstOrDefaultAsync(
                x => x.Id == request.CategoryId &&
                     x.Type == CategoryType.Expense &&
                     x.IsActive,
                cancellationToken);

        if (category is null)
        {
            throw new ArgumentException(
                $"Expense category with ID {request.CategoryId} was not found.",
                nameof(request.CategoryId));
        }

        var expense = new Expense
        {
            FamilyId = request.FamilyId,
            FamilyMemberId = request.FamilyMemberId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            ExpenseType = request.ExpenseType.Trim(),
            PaymentMethod = request.PaymentMethod.Trim(),
            SpendingType = request.SpendingType.Trim(),
            IsEssential = request.IsEssential,
            IsRecurring = request.IsRecurring,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Expenses.Add(expense);

        await _context.SaveChangesAsync(cancellationToken);

        expense.Category = category;

        return MapToDto(expense);
    }

    #endregion

    #region Update

    public async Task<bool> UpdateAsync(
        int id,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpdateRequest(request);

        if (id <= 0)
            return false;

        var category = await _context.Categories
            .FirstOrDefaultAsync(
                x => x.Id == request.CategoryId &&
                     x.Type == CategoryType.Expense &&
                     x.IsActive,
                cancellationToken);

        if (category is null)
        {
            throw new ArgumentException(
                $"Expense category with ID {request.CategoryId} was not found.",
                nameof(request.CategoryId));
        }

        var expense = await _context.Expenses
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (expense is null)
            return false;

        expense.FamilyId = request.FamilyId;
        expense.FamilyMemberId = request.FamilyMemberId;
        expense.CategoryId = request.CategoryId;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.ExpenseType = request.ExpenseType.Trim();
        expense.PaymentMethod = request.PaymentMethod.Trim();
        expense.SpendingType = request.SpendingType.Trim();
        expense.IsEssential = request.IsEssential;
        expense.IsRecurring = request.IsRecurring;
        expense.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Delete

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        var expense = await _context.Expenses
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (expense is null)
            return false;

        _context.Expenses.Remove(expense);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion

    #region Mapping

    private static ExpenseDto MapToDto(Expense expense)
    {
        return new ExpenseDto(
            expense.Id,
            expense.FamilyId,
            expense.FamilyMemberId,
            expense.CategoryId,
            expense.Amount,
            expense.ExpenseDate,
            expense.ExpenseType,
            expense.PaymentMethod,
            expense.SpendingType,
            expense.IsEssential,
            expense.IsRecurring,
            expense.Notes,
            expense.CreatedAt,
            expense.UpdatedAt,
            expense.Category?.Name);
    }

    #endregion

    #region Validation

    private static void ValidateCreateRequest(
        CreateExpenseRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.FamilyId <= 0)
            throw new ArgumentException(
                "Family ID is required.");

        if (request.FamilyMemberId <= 0)
            throw new ArgumentException(
                "Family Member ID is required.");

        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException(
                "Category ID is required.");

        if (request.Amount <= 0)
            throw new ArgumentException(
                "Amount must be greater than zero.");

        if (request.ExpenseDate > DateTime.UtcNow)
            throw new ArgumentException(
                "Expense date cannot be in the future.");

        if (string.IsNullOrWhiteSpace(request.ExpenseType))
            throw new ArgumentException(
                "Expense type is required.");

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException(
                "Payment method is required.");

        if (string.IsNullOrWhiteSpace(request.SpendingType))
            throw new ArgumentException(
                "Spending type is required.");
    }

    private static void ValidateUpdateRequest(
        UpdateExpenseRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.FamilyId <= 0)
            throw new ArgumentException(
                "Family ID is required.");

        if (request.FamilyMemberId <= 0)
            throw new ArgumentException(
                "Family Member ID is required.");

        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException(
                "Category ID is required.");

        if (request.Amount <= 0)
            throw new ArgumentException(
                "Amount must be greater than zero.");

        if (request.ExpenseDate > DateTime.UtcNow)
            throw new ArgumentException(
                "Expense date cannot be in the future.");

        if (string.IsNullOrWhiteSpace(request.ExpenseType))
            throw new ArgumentException(
                "Expense type is required.");

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException(
                "Payment method is required.");

        if (string.IsNullOrWhiteSpace(request.SpendingType))
            throw new ArgumentException(
                "Spending type is required.");
    }

    #endregion
}