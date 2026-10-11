namespace GharMitra.Domain.Entities;

public class FamilyMember
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsFamilyHead { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
}