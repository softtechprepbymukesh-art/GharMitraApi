using GharMitra.Application.Families.DTOs;
using GharMitra.Infrastructure.Services;

namespace GharMitra.Tests.Services;

public class FamilyServiceValidationTests
{
    private readonly FamilyService _service = new(new ThrowingConnectionFactory());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public Task CreateFamily_BlankName_Rejected(string name)
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateFamilyRequest { Name = name }));

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
}
