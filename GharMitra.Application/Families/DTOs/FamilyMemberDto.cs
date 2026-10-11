namespace GharMitra.Application.Families.DTOs;

public sealed record FamilyMemberDto(
    int Id,
    int FamilyId,
    string Name,
    string Role,
    bool IsFamilyHead,
    bool IsActive,
    DateTime CreatedAt);
