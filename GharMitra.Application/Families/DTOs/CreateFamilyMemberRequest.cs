using System.ComponentModel.DataAnnotations;

namespace GharMitra.Application.Families.DTOs;

public sealed class CreateFamilyMemberRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    public bool IsFamilyHead { get; set; }
}
