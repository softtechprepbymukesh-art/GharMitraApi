using GharMitra.Domain.Entities;

namespace GharMitra.Application.Categories.DTOs;

public sealed record UpdateCategoryRequest(
    string Name,
    CategoryType Type,
    string? Description,
    int DisplayOrder = 0);