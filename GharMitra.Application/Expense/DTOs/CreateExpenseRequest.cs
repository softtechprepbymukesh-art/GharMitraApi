using System.ComponentModel.DataAnnotations;
using GharMitra.Domain.Entities;

namespace GharMitra.Application.Expenses.DTOs;

/// <summary>
/// FamilyId and FamilyMemberId are deliberately absent: they come from the
/// authenticated caller, never from the request. See Docs/GAP_ANALYSIS.md C2.
/// </summary>
public sealed class CreateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    /// <summary>Optional finer grouping, e.g. Grocery -> Household.</summary>
    public Guid? SubCategoryId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    /// <summary>1 = Need, 2 = Want.</summary>
    public NeedOrWant NeedOrWant { get; set; } = NeedOrWant.Need;

    [Required]
    public Guid PaymentMethodId { get; set; }

    [Required]
    public Guid SpendingTypeId { get; set; }

    public bool IsEssential { get; set; }

    public bool IsRecurring { get; set; }

    /// <summary>Which account the money left. Optional until accounts are set up.</summary>
    public Guid? AccountId { get; set; }

    public string? Notes { get; set; }
}
