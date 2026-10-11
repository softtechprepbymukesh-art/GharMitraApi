namespace GharMitra.Api.DTOs.Expense;

public class ExpenseUpdateDto
{
    public int FamilyMemberId { get; set; }

    public int CategoryId { get; set; }

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string ExpenseType { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public string SpendingType { get; set; } = string.Empty;

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    public string? Notes { get; set; }
}