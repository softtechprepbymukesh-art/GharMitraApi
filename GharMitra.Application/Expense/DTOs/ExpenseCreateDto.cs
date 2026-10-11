using System.ComponentModel.DataAnnotations;

namespace GharMitra.Api.DTOs.Expense;

public class ExpenseCreateDto
{
    [Required]
    public int FamilyId { get; set; }

    [Required]
    public int FamilyMemberId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string ExpenseType { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SpendingType { get; set; } = string.Empty;

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}