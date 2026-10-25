using GharMitra.Application.Incomes.DTOs;
using GharMitra.Application.Incomes.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class IncomeController : ControllerBase
{
    private readonly IIncomeService _incomeService;
    private readonly ILogger<IncomeController> _logger;

    public IncomeController(IIncomeService incomeService, ILogger<IncomeController> logger)
    {
        _incomeService = incomeService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active incomes
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncomeDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching all active incomes");
        var incomes = await _incomeService.GetAllAsync(cancellationToken);
        return Ok(incomes);
    }

    /// <summary>
    /// Get income by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncomeDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching income with ID: {IncomeId}", id);
        var income = await _incomeService.GetByIdAsync(id, cancellationToken);

        if (income is null)
        {
            _logger.LogWarning("Income with ID: {IncomeId} not found", id);
            return NotFound(new { message = $"Income with ID {id} not found." });
        }

        return Ok(income);
    }

    /// <summary>
    /// Get incomes by category ID
    /// </summary>
    [HttpGet("category/{categoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncomeDto>>> GetByCategoryId(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching incomes for category ID: {CategoryId}", categoryId);
        var incomes = await _incomeService.GetByCategoryIdAsync(categoryId, cancellationToken);
        return Ok(incomes);
    }

    /// <summary>
    /// Create new income
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncomeDto>> Create(
        CreateIncomeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating new income from source: {SourceName}", request.SourceName);
            var income = await _incomeService.CreateAsync(request, cancellationToken);
            _logger.LogInformation("Income created successfully with ID: {IncomeId}", income.Id);
            return CreatedAtAction(nameof(GetById), new { id = income.Id }, income);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error creating income: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating income");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating income." });
        }
    }

    /// <summary>
    /// Update existing income
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateIncomeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating income with ID: {IncomeId}", id);
            var result = await _incomeService.UpdateAsync(id, request, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Income with ID: {IncomeId} not found for update", id);
                return NotFound(new { message = $"Income with ID {id} not found." });
            }

            _logger.LogInformation("Income with ID: {IncomeId} updated successfully", id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating income: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating income with ID: {IncomeId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while updating income." });
        }
    }

    /// <summary>
    /// Delete income by ID
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting income with ID: {IncomeId}", id);
        var result = await _incomeService.DeleteAsync(id, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Income with ID: {IncomeId} not found for deletion", id);
            return NotFound(new { message = $"Income with ID {id} not found." });
        }

        _logger.LogInformation("Income with ID: {IncomeId} deleted successfully", id);
        return NoContent();
    }

    /// <summary>
    /// Deactivate income by ID
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating income with ID: {IncomeId}", id);
        var result = await _incomeService.DeactivateAsync(id, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Income with ID: {IncomeId} not found for deactivation", id);
            return NotFound(new { message = $"Income with ID {id} not found." });
        }

        _logger.LogInformation("Income with ID: {IncomeId} deactivated successfully", id);
        return NoContent();
    }

    /// <summary>
    /// Activate income by ID
    /// </summary>
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activating income with ID: {IncomeId}", id);
        var result = await _incomeService.ActivateAsync(id, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Income with ID: {IncomeId} not found for activation", id);
            return NotFound(new { message = $"Income with ID {id} not found." });
        }

        _logger.LogInformation("Income with ID: {IncomeId} activated successfully", id);
        return NoContent();
    }
}