using GharMitra.Application.Categories.DTOs;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Services;

namespace GharMitra.Tests.Services;

public class CategoryServiceValidationTests
{
    private readonly CategoryService _service = new(new ThrowingConnectionFactory(), FakeCurrentUser.Head());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public Task Create_BlankName_RejectedBeforeDatabase(string name)
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateCategoryRequest(name, CategoryType.Expense, null)));
}
