using System.ComponentModel.DataAnnotations;

namespace GharMitra.Application.Expenses.DTOs;

public sealed class CreateExpenseRequest
{
    [Required]
    public int FamilyId { get; set; }

    [Required]
    public int FamilyMemberId { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    [Required]
    public string ExpenseType { get; set; } = string.Empty;

    [Required]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    public string SpendingType { get; set; } = string.Empty;

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    public string? Notes { get; set; }
}