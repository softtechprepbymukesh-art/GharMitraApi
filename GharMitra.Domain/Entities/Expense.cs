namespace GharMitra.Domain.Entities;

public class Expense
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public int FamilyMemberId { get; set; }

    // Category.Id is Guid, therefore FK must also be Guid
    public Guid CategoryId { get; set; }

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string ExpenseType { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public string SpendingType { get; set; } = string.Empty;

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties

    public Family Family { get; set; } = null!;

    public FamilyMember FamilyMember { get; set; } = null!;

    public Category Category { get; set; } = null!;
}