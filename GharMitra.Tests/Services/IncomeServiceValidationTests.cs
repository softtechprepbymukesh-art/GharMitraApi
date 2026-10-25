using GharMitra.Application.Incomes.DTOs;
using GharMitra.Infrastructure.Services;

namespace GharMitra.Tests.Services;

public class IncomeServiceValidationTests
{
    private readonly IncomeService _service = new(new ThrowingConnectionFactory());
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static DateTime Past => DateTime.UtcNow.AddDays(-1);

    [Fact]
    public Task Create_BlankSource_RejectedBeforeDatabase()
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateIncomeRequest(" ", 100m, Past, CategoryId)));

    [Fact]
    public Task Create_ZeroAmount_RejectedBeforeDatabase()
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateIncomeRequest("Salary", 0m, Past, CategoryId)));

    [Fact]
    public Task Create_FutureDate_RejectedBeforeDatabase()
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateIncomeRequest("Salary", 10m, DateTime.UtcNow.AddDays(3), CategoryId)));

    [Fact]
    public Task Create_EmptyCategory_RejectedBeforeDatabase()
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateIncomeRequest("Salary", 10m, Past, Guid.Empty)));

    [Fact]
    public Task Update_NegativeAmount_RejectedBeforeDatabase()
        => Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), new UpdateIncomeRequest("Salary", -1m, Past, CategoryId)));
}
