namespace GharMitra.Api.DTOs.Expense;

public class ExpenseResponseDto
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public int FamilyMemberId { get; set; }

    public string? FamilyMemberName { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string ExpenseType { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public string SpendingType { get; set; } = string.Empty;

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}