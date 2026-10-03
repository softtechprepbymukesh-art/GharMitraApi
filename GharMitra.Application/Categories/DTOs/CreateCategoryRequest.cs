using GharMitra.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace GharMitra.Application.Categories.DTOs
{
    public sealed record CreateCategoryRequest(
    string Name,
    CategoryType Type,
    string? Description,
    int DisplayOrder = 0);
}
