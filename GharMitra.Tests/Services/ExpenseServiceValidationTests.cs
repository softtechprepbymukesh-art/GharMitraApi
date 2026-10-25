using GharMitra.Application.Expenses.DTOs;
using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Services;

namespace GharMitra.Tests.Services;

public class ExpenseServiceValidationTests
{
    private readonly ExpenseService _service = new(new ThrowingConnectionFactory(), FakeCurrentUser.Head());

    private static CreateExpenseRequest ValidCreate() => new()
    {
        CategoryId = Guid.NewGuid(),
        Amount = 100m,
        ExpenseDate = DateTime.UtcNow.AddDays(-1),
        NeedOrWant = NeedOrWant.Need,
        PaymentMethodId = Guid.NewGuid(),
        SpendingTypeId = Guid.NewGuid()
    };

    private static UpdateExpenseRequest ValidUpdate() => new()
    {
        CategoryId = Guid.NewGuid(),
        Amount = 10m,
        ExpenseDate = DateTime.UtcNow.AddDays(-1),
        NeedOrWant = NeedOrWant.Want,
        PaymentMethodId = Guid.NewGuid(),
        SpendingTypeId = Guid.NewGuid()
    };

    [Fact]
    public Task Create_NullRequest_Throws()
        => Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(null!));

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

    /// <summary>
    /// Regression for Docs/GAP_ANALYSIS.md H7: at 00:30 IST the UTC clock still
    /// reads yesterday, so a "today" expense must not be rejected as future-dated.
    /// </summary>
    [Fact]
    public async Task Create_TodayInIndianTime_IsNotTreatedAsFutureDated()
    {
        var request = ValidCreate();
        request.ExpenseDate = DateTime.UtcNow.AddHours(5.5).Date;

        // It still fails - there is no database behind this service - but it
        // must fail on the connection, never on the date.
        var ex = await Record.ExceptionAsync(() => _service.CreateAsync(request));

        Assert.NotNull(ex);
        Assert.DoesNotContain("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public Task Create_MissingPaymentMethod_Rejected()
    {
        var request = ValidCreate();
        request.PaymentMethodId = Guid.Empty;
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public Task Create_MissingSpendingType_Rejected()
    {
        var request = ValidCreate();
        request.SpendingTypeId = Guid.Empty;
        return Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public Task Create_InvalidNeedOrWant_Rejected()
    {
        var request = ValidCreate();
        request.NeedOrWant = (NeedOrWant)9;
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
        => Assert.False(await _service.UpdateAsync(0, ValidUpdate()));

    [Fact]
    public async Task AnonymousCaller_CannotReadAnything()
    {
        var service = new ExpenseService(new ThrowingConnectionFactory(), FakeCurrentUser.Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync());
    }

    [Fact]
    public async Task CallerWithoutRecordPermission_CannotWrite()
    {
        // A Viewer holds no expense.record.own permission.
        var viewer = new FakeCurrentUser(1, 1, "Viewer", []);
        var service = new ExpenseService(new ThrowingConnectionFactory(), viewer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(ValidCreate()));
    }
}
