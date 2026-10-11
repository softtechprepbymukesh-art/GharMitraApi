using System.ComponentModel.DataAnnotations;

namespace GharMitra.Application.Families.DTOs;

public sealed class CreateFamilyRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}
