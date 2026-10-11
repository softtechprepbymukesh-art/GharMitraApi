using GharMitra.Application.Families.DTOs;
using GharMitra.Application.Families.Interfaces;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Services;

public sealed class FamilyService : IFamilyService
{
    private readonly GharMitraDbContext _context;

    public FamilyService(GharMitraDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<FamilyDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var families = await _context.Families
            .AsNoTracking()
            .Include(x => x.Members)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return families.Select(MapToDto).ToList();
    }

    public async Task<FamilyDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        var family = await _context.Families
            .AsNoTracking()
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return family is null ? null : MapToDto(family);
    }

    public async Task<FamilyDto> CreateAsync(
        CreateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(request.Name);

        var family = new Family
        {
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Families.Add(family);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(family);
    }

    public async Task<bool> UpdateAsync(
        int id,
        UpdateFamilyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        var family = await _context.Families
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (family is null)
            return false;

        family.Name = ValidateName(request.Name);
        family.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        var family = await _context.Families
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (family is null)
            return false;

        family.IsActive = false;
        foreach (var member in family.Members)
        {
            member.IsActive = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FamilyMemberDto>> GetMembersAsync(
        int familyId,
        CancellationToken cancellationToken = default)
    {
        if (familyId <= 0)
            return Array.Empty<FamilyMemberDto>();

        var members = await _context.FamilyMembers
            .AsNoTracking()
            .Where(x => x.FamilyId == familyId)
            .OrderByDescending(x => x.IsFamilyHead)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return members.Select(MapToMemberDto).ToList();
    }

    public async Task<FamilyMemberDto> AddMemberAsync(
        int familyId,
        CreateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await GetActiveFamilyAsync(familyId, cancellationToken);

        var member = new FamilyMember
        {
            FamilyId = family.Id,
            Name = ValidateName(request.Name),
            Role = ValidateRole(request.Role),
            IsFamilyHead = request.IsFamilyHead,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (member.IsFamilyHead)
        {
            await ClearFamilyHeadAsync(family.Id, excludeMemberId: null, cancellationToken);
        }

        _context.FamilyMembers.Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToMemberDto(member);
    }

    public async Task<bool> UpdateMemberAsync(
        int familyId,
        int memberId,
        UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (familyId <= 0 || memberId <= 0)
            return false;

        var member = await _context.FamilyMembers
            .FirstOrDefaultAsync(
                x => x.Id == memberId && x.FamilyId == familyId,
                cancellationToken);

        if (member is null)
            return false;

        member.Name = ValidateName(request.Name);
        member.Role = ValidateRole(request.Role);
        member.IsActive = request.IsActive;

        if (request.IsFamilyHead && !member.IsFamilyHead)
        {
            await ClearFamilyHeadAsync(familyId, member.Id, cancellationToken);
        }

        member.IsFamilyHead = request.IsFamilyHead;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteMemberAsync(
        int familyId,
        int memberId,
        CancellationToken cancellationToken = default)
    {
        if (familyId <= 0 || memberId <= 0)
            return false;

        var member = await _context.FamilyMembers
            .FirstOrDefaultAsync(
                x => x.Id == memberId && x.FamilyId == familyId,
                cancellationToken);

        if (member is null)
            return false;

        member.IsActive = false;
        member.IsFamilyHead = false;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Family> GetActiveFamilyAsync(
        int familyId,
        CancellationToken cancellationToken)
    {
        if (familyId <= 0)
            throw new ArgumentException("Family ID is required.");

        var family = await _context.Families
            .FirstOrDefaultAsync(x => x.Id == familyId, cancellationToken);

        if (family is null)
            throw new ArgumentException($"Family with ID {familyId} was not found.");

        if (!family.IsActive)
            throw new ArgumentException($"Family with ID {familyId} is inactive.");

        return family;
    }

    private async Task ClearFamilyHeadAsync(
        int familyId,
        int? excludeMemberId,
        CancellationToken cancellationToken)
    {
        var heads = await _context.FamilyMembers
            .Where(x =>
                x.FamilyId == familyId &&
                x.IsFamilyHead &&
                (excludeMemberId == null || x.Id != excludeMemberId))
            .ToListAsync(cancellationToken);

        foreach (var head in heads)
        {
            head.IsFamilyHead = false;
        }
    }

    private static FamilyDto MapToDto(Family family)
    {
        return new FamilyDto(
            family.Id,
            family.Name,
            family.IsActive,
            family.CreatedAt,
            family.Members
                .OrderByDescending(x => x.IsFamilyHead)
                .ThenBy(x => x.Name)
                .Select(MapToMemberDto)
                .ToList());
    }

    private static FamilyMemberDto MapToMemberDto(FamilyMember member)
    {
        return new FamilyMemberDto(
            member.Id,
            member.FamilyId,
            member.Name,
            member.Role,
            member.IsFamilyHead,
            member.IsActive,
            member.CreatedAt);
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        return name.Trim();
    }

    private static string ValidateRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required.");

        return role.Trim();
    }
}
