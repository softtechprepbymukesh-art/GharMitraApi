namespace GharMitra.Domain.Entities;

public class Income
{
    public Guid Id { get; private set; }
    public string SourceName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime IncomeDate { get; private set; }
    public Guid CategoryId { get; private set; }  // Foreign Key
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation property
    public Category? Category { get; private set; }

    private Income() { }

    public Income(
        string sourceName,
        decimal amount,
        DateTime incomeDate,
        Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name is required.", nameof(sourceName));
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (incomeDate > DateTime.UtcNow)
            throw new ArgumentException("Income date cannot be in the future.", nameof(incomeDate));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.", nameof(categoryId));

        Id = Guid.NewGuid();
        SourceName = sourceName.Trim();
        Amount = amount;
        IncomeDate = incomeDate;
        CategoryId = categoryId;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(
        string sourceName,
        decimal amount,
        DateTime incomeDate,
        Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name is required.", nameof(sourceName));
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (incomeDate > DateTime.UtcNow)
            throw new ArgumentException("Income date cannot be in the future.", nameof(incomeDate));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.", nameof(categoryId));

        SourceName = sourceName.Trim();
        Amount = amount;
        IncomeDate = incomeDate;
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}