namespace GharMitra.Application.Families.DTOs;

public sealed record FamilyDto(
    int Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<FamilyMemberDto> Members);
