using GharMitra.Application.Expenses.DTOs;
using GharMitra.Infrastructure.Services;

namespace GharMitra.Tests.Services;

public class ExpenseServiceValidationTests
{
    private readonly ExpenseService _service = new(new ThrowingConnectionFactory());

    private static CreateExpenseRequest ValidCreate() => new()
    {
        FamilyId = 1,
        FamilyMemberId = 1,
        CategoryId = Guid.NewGuid(),
        Amount = 100m,
        ExpenseDate = DateTime.UtcNow.AddDays(-1),
        ExpenseType = "Need",
        PaymentMethod = "UPI",
        SpendingType = "Monthly"
    };

    [Fact]
    public Task Create_NullRequest_Throws()
        => Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(null!));

    [Fact]
    public Task Create_InvalidFamily_Rejected()
    {
        var request = ValidCreate();
        request.FamilyId = 0;
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public Task Create_InvalidMember_Rejected()
    {
        var request = ValidCreate();
        request.FamilyMemberId = 0;
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public Task Create_EmptyCategory_Rejected()
    {
        var request = ValidCreate();
        request.CategoryId = Guid.Empty;
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public Task Create_ZeroAmount_Rejected()
    {
        var request = ValidCreate();
        request.Amount = 0m;
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public Task Create_FutureDate_Rejected()
    {
        var request = ValidCreate();
        request.ExpenseDate = DateTime.UtcNow.AddDays(5);
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Theory]
    [InlineData("ExpenseType")]
    [InlineData("PaymentMethod")]
    [InlineData("SpendingType")]
    public Task Create_BlankTextField_Rejected(string field)
    {
        var request = ValidCreate();
        switch (field)
        {
            case "ExpenseType": request.ExpenseType = " "; break;
            case "PaymentMethod": request.PaymentMethod = ""; break;
            default: request.SpendingType = " "; break;
        }
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task GuardClauses_ReturnEmptyResultsWithoutDatabase()
    {
        Assert.Null(await _service.GetByIdAsync(0));
        Assert.Empty(await _service.GetByCategoryIdAsync(Guid.Empty));
        Assert.Equal(0m, await _service.GetTotalExpensesByCategoryAsync(Guid.Empty));
        Assert.False(await _service.DeleteAsync(0));
    }

    [Fact]
    public async Task Update_InvalidId_ReturnsFalseWithoutDatabase()
    {
        var update = new UpdateExpenseRequest
        {
            FamilyId = 1,
            FamilyMemberId = 1,
            CategoryId = Guid.NewGuid(),
            Amount = 10m,
            ExpenseDate = DateTime.UtcNow.AddDays(-1),
            ExpenseType = "Need",
            PaymentMethod = "UPI",
            SpendingType = "Monthly"
        };

        Assert.False(await _service.UpdateAsync(0, update));
    }
}
