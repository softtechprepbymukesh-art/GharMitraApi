using GharMitra.Application.Categories.DTOs;
using GharMitra.Application.Categories.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly GharMitraDbContext _context;

    public CategoryService(GharMitraDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryDto(
                x.Id,
                x.Name,
                x.Description,
                x.Type,
                x.IsActive,
                x.DisplayOrder,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CategoryDto(
                x.Id,
                x.Name,
                x.Description,
                x.Type,
                x.IsActive,
                x.DisplayOrder,
                x.CreatedAt,
                x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.",
                nameof(request.Name));
        }

        var exists = await _context.Categories
            .AnyAsync(
                x => x.Name == name && x.Type == request.Type,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Category '{name}' already exists for {request.Type}.");
        }

        var category = new Category(
            name,
            request.Type,
            request.Description?.Trim(),
            request.DisplayOrder);

        _context.Categories.Add(category);

        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.Type,
            category.IsActive,
            category.DisplayOrder,
            category.CreatedAt,
            category.UpdatedAt);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.",
                nameof(request.Name));
        }

        var exists = await _context.Categories
            .AnyAsync(
                x => x.Id != id &&
                     x.Name == name &&
                     x.Type == request.Type,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Category '{name}' already exists for {request.Type}.");
        }

        category.Update(
            name,
            request.Description?.Trim(),
            request.Type,
            request.DisplayOrder);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        // Soft delete: category database se physically delete nahi hogi.
        category.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}