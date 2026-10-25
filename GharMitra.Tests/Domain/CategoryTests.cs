using GharMitra.Domain.Entities;

namespace GharMitra.Tests.Domain;

public class CategoryTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var category = new Category("Groceries", CategoryType.Expense, "Food", 2);

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Groceries", category.Name);
        Assert.Equal(CategoryType.Expense, category.Type);
        Assert.Equal("Food", category.Description);
        Assert.Equal(2, category.DisplayOrder);
        Assert.True(category.IsActive);
        Assert.Null(category.UpdatedAt);
    }

    [Fact]
    public void Update_ChangesValuesAndSetsUpdatedAt()
    {
        var category = new Category("Groceries", CategoryType.Expense);

        category.Update("Salary", "Monthly", CategoryType.Income, 5);

        Assert.Equal("Salary", category.Name);
        Assert.Equal("Monthly", category.Description);
        Assert.Equal(CategoryType.Income, category.Type);
        Assert.Equal(5, category.DisplayOrder);
        Assert.NotNull(category.UpdatedAt);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesFlag()
    {
        var category = new Category("Groceries", CategoryType.Expense);

        category.Deactivate();
        Assert.False(category.IsActive);

        category.Activate();
        Assert.True(category.IsActive);
    }

    [Fact]
    public void CategoryType_HasStableValues()
    {
        // These numbers are persisted in the database (Categories.Type).
        Assert.Equal(1, (int)CategoryType.Expense);
        Assert.Equal(2, (int)CategoryType.Income);
    }
}
