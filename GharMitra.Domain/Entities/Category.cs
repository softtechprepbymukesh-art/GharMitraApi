namespace GharMitra.Domain.Entities;

public class Category
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CategoryType Type { get; private set; }

    public bool IsActive { get; private set; }

    public int DisplayOrder { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    private Category()
    {
    }

    public Category(
        string name,
        CategoryType type,
        string? description = null,
        int displayOrder = 0)
    {
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        Description = description;
        DisplayOrder = displayOrder;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? description,
        CategoryType type,
        int displayOrder)
    {
        Name = name;
        Description = description;
        Type = type;
        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum CategoryType
{
    Expense = 1,
    Income = 2
}
