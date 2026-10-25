using GharMitra.Application.Expenses.DTOs;
using GharMitra.Application.Expenses.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpenseController> _logger;

    public ExpenseController(
        IExpenseService expenseService,
        ILogger<ExpenseController> logger)
    {
        _expenseService = expenseService
            ?? throw new ArgumentNullException(nameof(expenseService));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    #region GET Endpoints

    /// <summary>
    /// Get all expenses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching all expenses");

            var expenses = await _expenseService.GetAllAsync(
                cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} expenses",
                expenses.Count);

            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching expenses");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while fetching expenses."
                });
        }
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Fetching expense with ID: {ExpenseId}",
                id);

            var expense = await _expenseService.GetByIdAsync(
                id,
                cancellationToken);

            if (expense is null)
            {
                _logger.LogWarning(
                    "Expense with ID: {ExpenseId} not found",
                    id);

                return NotFound(new
                {
                    message = $"Expense with ID {id} not found."
                });
            }

            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching expense with ID: {ExpenseId}",
                id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while fetching expense."
                });
        }
    }

    /// <summary>
    /// Get expenses by category
    /// </summary>
    [HttpGet("category/{categoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> GetByCategoryId(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Fetching expenses for category ID: {CategoryId}",
                categoryId);

            var expenses = await _expenseService.GetByCategoryIdAsync(
                categoryId,
                cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} expenses for category",
                expenses.Count);

            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching expenses for category ID: {CategoryId}",
                categoryId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while fetching expenses."
                });
        }
    }

    /// <summary>
    /// Get total expenses by category
    /// </summary>
    [HttpGet("category/{categoryId:guid}/total")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetTotalByCategory(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Fetching total expenses for category ID: {CategoryId}",
                categoryId);

            var total = await _expenseService
                .GetTotalExpensesByCategoryAsync(
                    categoryId,
                    cancellationToken);

            _logger.LogInformation(
                "Total expenses for category: {Total}",
                total);

            return Ok(new
            {
                categoryId,
                total
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calculating total expenses for category ID: {CategoryId}",
                categoryId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while calculating total."
                });
        }
    }

    #endregion


    #region POST Endpoint

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> Create(
        [FromBody] CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Creating new expense, Amount: {Amount}",
                request.Amount);

            var expense = await _expenseService.CreateAsync(
                request,
                cancellationToken);

            _logger.LogInformation(
                "Expense created successfully with ID: {ExpenseId}",
                expense.Id);

            return CreatedAtAction(
                nameof(GetById),
                new { id = expense.Id },
                expense);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "Validation error creating expense: {Message}",
                ex.Message);

            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating expense");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while creating expense."
                });
        }
    }

    #endregion


    #region PUT Endpoint

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Updating expense with ID: {ExpenseId}",
                id);

            var result = await _expenseService.UpdateAsync(
                id,
                request,
                cancellationToken);

            if (!result)
            {
                _logger.LogWarning(
                    "Expense with ID: {ExpenseId} not found for update",
                    id);

                return NotFound(new
                {
                    message = $"Expense with ID {id} not found."
                });
            }

            _logger.LogInformation(
                "Expense with ID: {ExpenseId} updated successfully",
                id);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "Validation error updating expense: {Message}",
                ex.Message);

            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating expense with ID: {ExpenseId}",
                id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while updating expense."
                });
        }
    }

    #endregion


    #region DELETE Endpoint

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Deleting expense with ID: {ExpenseId}",
                id);

            var result = await _expenseService.DeleteAsync(
                id,
                cancellationToken);

            if (!result)
            {
                _logger.LogWarning(
                    "Expense with ID: {ExpenseId} not found for deletion",
                    id);

                return NotFound(new
                {
                    message = $"Expense with ID {id} not found."
                });
            }

            _logger.LogInformation(
                "Expense with ID: {ExpenseId} deleted successfully",
                id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting expense with ID: {ExpenseId}",
                id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "An error occurred while deleting expense."
                });
        }
    }

    #endregion
}