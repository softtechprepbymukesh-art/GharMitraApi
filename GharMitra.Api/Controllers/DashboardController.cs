using GharMitra.Application.Planning.DTOs;
using GharMitra.Application.Planning.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Controllers;

/// <summary>
/// Stage 4 and 5 of the functional flow: the family's monthly picture and the
/// affordability question. Everything here is calculated on request; none of
/// it is stored.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Income, spending, commitments and Safe-To-Spend for a month.
    /// Defaults to the current month in the family's own time zone.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
        => Ok(await _dashboardService.GetDashboardAsync(year, month, cancellationToken));

    /// <summary>
    /// "Can I afford this?" Returns a yes or no plus, when the answer is no,
    /// what the family could change to make it work.
    /// </summary>
    [HttpPost("affordability")]
    [ProducesResponseType(typeof(AffordabilityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CanAfford(
        [FromBody] AffordabilityRequest request,
        CancellationToken cancellationToken)
        => Ok(await _dashboardService.CanAffordAsync(request, cancellationToken));
}
