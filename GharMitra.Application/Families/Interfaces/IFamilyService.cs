using GharMitra.Application.Families.DTOs;

namespace GharMitra.Application.Families.Interfaces;

public interface IFamilyService
{
    Task<IReadOnlyList<FamilyDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<FamilyDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<FamilyDto> CreateAsync(
        CreateFamilyRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        int id,
        UpdateFamilyRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyMemberDto>> GetMembersAsync(
        int familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyMemberDto> AddMemberAsync(
        int familyId,
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateMemberAsync(
        int familyId,
        int memberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMemberAsync(
        int familyId,
        int memberId,
        CancellationToken cancellationToken = default);
}
