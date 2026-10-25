using GharMitra.Domain.Entities;

namespace GharMitra.Tests.Domain;

public class IncomeTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidInput_CreatesActiveIncomeWithTrimmedName()
    {
        var income = new Income("  Salary  ", 75000m, DateTime.UtcNow.AddDays(-1), CategoryId);

        Assert.NotEqual(Guid.Empty, income.Id);
        Assert.Equal("Salary", income.SourceName);
        Assert.Equal(75000m, income.Amount);
        Assert.Equal(CategoryId, income.CategoryId);
        Assert.True(income.IsActive);
        Assert.Null(income.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankSourceName_Throws(string name)
        => Assert.Throws<ArgumentException>(
            () => new Income(name, 100m, DateTime.UtcNow.AddDays(-1), CategoryId));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_NonPositiveAmount_Throws(int amount)
        => Assert.Throws<ArgumentException>(
            () => new Income("Salary", amount, DateTime.UtcNow.AddDays(-1), CategoryId));

    [Fact]
    public void Constructor_FutureDate_Throws()
        => Assert.Throws<ArgumentException>(
            () => new Income("Salary", 100m, DateTime.UtcNow.AddDays(2), CategoryId));

    [Fact]
    public void Constructor_EmptyCategory_Throws()
        => Assert.Throws<ArgumentException>(
            () => new Income("Salary", 100m, DateTime.UtcNow.AddDays(-1), Guid.Empty));

    [Fact]
    public void Deactivate_ThenActivate_TogglesFlagAndSetsUpdatedAt()
    {
        var income = new Income("Salary", 100m, DateTime.UtcNow.AddDays(-1), CategoryId);

        income.Deactivate();
        Assert.False(income.IsActive);
        Assert.NotNull(income.UpdatedAt);

        income.Activate();
        Assert.True(income.IsActive);
    }

    [Fact]
    public void Update_ValidInput_ChangesValues()
    {
        var income = new Income("Salary", 100m, DateTime.UtcNow.AddDays(-1), CategoryId);
        var newCategory = Guid.NewGuid();

        income.Update(" Rent ", 500m, DateTime.UtcNow.AddDays(-2), newCategory);

        Assert.Equal("Rent", income.SourceName);
        Assert.Equal(500m, income.Amount);
        Assert.Equal(newCategory, income.CategoryId);
        Assert.NotNull(income.UpdatedAt);
    }

    [Fact]
    public void Update_InvalidAmount_Throws()
    {
        var income = new Income("Salary", 100m, DateTime.UtcNow.AddDays(-1), CategoryId);

        Assert.Throws<ArgumentException>(
            () => income.Update("Salary", 0m, DateTime.UtcNow.AddDays(-1), CategoryId));
    }
}
