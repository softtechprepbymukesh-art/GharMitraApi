using GharMitra.Domain.Entities;

namespace GharMitra.Application.Categories.DTOs
{
public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    CategoryType Type,
    bool IsActive,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
}