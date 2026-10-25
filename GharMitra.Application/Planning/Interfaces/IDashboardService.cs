using GharMitra.Application.Planning.DTOs;

namespace GharMitra.Application.Planning.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// Builds the current month's picture for the caller's family, scoped by
    /// their role: the Family Head sees the whole family, anyone else sees
    /// only their own money.
    /// </summary>
    Task<DashboardDto> GetDashboardAsync(
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default);

    Task<AffordabilityDto> CanAffordAsync(
        AffordabilityRequest request,
        CancellationToken cancellationToken = default);
}
