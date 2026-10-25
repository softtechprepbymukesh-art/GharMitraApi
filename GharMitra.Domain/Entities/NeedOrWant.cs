namespace GharMitra.Domain.Entities;

/// <summary>
/// Requirement 6: every expense is classified as a need or a want.
///
/// A closed set of two values that will never grow, so it lives in code rather
/// than a lookup table. It is the axis the AI uses to answer "which expenses
/// can be reduced" — <see cref="Want"/> spending is the reducible part.
/// Stored in Expenses.NeedOrWant.
/// </summary>
public enum NeedOrWant : byte
{
    Need = 1,
    Want = 2
}
