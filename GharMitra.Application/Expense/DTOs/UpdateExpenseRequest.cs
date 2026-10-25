using System.ComponentModel.DataAnnotations;
using GharMitra.Domain.Entities;

namespace GharMitra.Application.Expenses.DTOs;

/// <summary>
/// FamilyId and FamilyMemberId are deliberately absent: they come from the
/// authenticated caller, never from the request. See Docs/GAP_ANALYSIS.md C2.
/// </summary>
public sealed class UpdateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    public Guid? SubCategoryId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public NeedOrWant NeedOrWant { get; set; } = NeedOrWant.Need;

    [Required]
    public Guid PaymentMethodId { get; set; }

    [Required]
    public Guid SpendingTypeId { get; set; }

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    public Guid? AccountId { get; set; }

    public string? Notes { get; set; }
}
