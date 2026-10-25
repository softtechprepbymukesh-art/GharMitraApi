using GharMitra.Application.Families.DTOs;
using GharMitra.Infrastructure.Services;

namespace GharMitra.Tests.Services;

public class FamilyServiceValidationTests
{
    private readonly FamilyService _service = new(new ThrowingConnectionFactory(), FakeCurrentUser.Head());

    /// <summary>
    /// A family is only ever created by registration, which also creates the
    /// Family Head login. A bare family would be a tenant nobody can sign in to.
    /// </summary>
    [Fact]
    public Task CreateFamily_IsNotAvailableOutsideRegistration()
        => Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new CreateFamilyRequest { Name = "Sharma" }));

    [Fact]
    public async Task GuardClauses_ReturnEmptyResultsWithoutDatabase()
    {
        Assert.Null(await _service.GetByIdAsync(0));
        Assert.Empty(await _service.GetMembersAsync(0));
        Assert.False(await _service.DeleteAsync(0));
        Assert.False(await _service.UpdateAsync(0, new UpdateFamilyRequest { Name = "X" }));
        Assert.False(await _service.DeleteMemberAsync(0, 1));
        Assert.False(await _service.DeleteMemberAsync(1, 0));
        Assert.False(await _service.UpdateMemberAsync(
            0, 1, new UpdateFamilyMemberRequest { Name = "X", Role = "Father" }));
    }

    /// <summary>Mandatory rule 4: another family's id must never resolve.</summary>
    [Fact]
    public async Task AnotherFamilyId_IsNeverVisible()
    {
        var service = new FamilyService(new ThrowingConnectionFactory(), FakeCurrentUser.Head(familyId: 1));

        Assert.Null(await service.GetByIdAsync(2));
        Assert.Empty(await service.GetMembersAsync(2));
        Assert.False(await service.DeleteAsync(2));
        Assert.False(await service.UpdateAsync(2, new UpdateFamilyRequest { Name = "Other" }));
        Assert.False(await service.DeleteMemberAsync(2, 1));
    }

    [Fact]
    public async Task AddingAMemberToAnotherFamily_IsRefused()
    {
        var service = new FamilyService(new ThrowingConnectionFactory(), FakeCurrentUser.Head(familyId: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddMemberAsync(2, new CreateFamilyMemberRequest { Name = "X", Role = "Son" }));
    }

    [Fact]
    public async Task NonHead_CannotManageTheFamily()
    {
        var service = new FamilyService(new ThrowingConnectionFactory(), FakeCurrentUser.Adult());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddMemberAsync(1, new CreateFamilyMemberRequest { Name = "X", Role = "Son" }));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateAsync(1, new UpdateFamilyRequest { Name = "Renamed" }));
    }
}
