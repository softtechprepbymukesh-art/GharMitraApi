using GharMitra.Application.Families.DTOs;
using GharMitra.Application.Families.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class FamilyController : ControllerBase
{
    private readonly IFamilyService _familyService;

    public FamilyController(IFamilyService familyService)
    {
        _familyService = familyService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var families = await _familyService.GetAllAsync(cancellationToken);
        return Ok(families);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamilyDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var family = await _familyService.GetByIdAsync(id, cancellationToken);
        if (family is null)
            return NotFound(new { message = $"Family with ID {id} not found." });

        return Ok(family);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyDto>> Create(
        [FromBody] CreateFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var family = await _familyService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = family.Id }, family);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _familyService.UpdateAsync(id, request, cancellationToken);
            if (!updated)
                return NotFound(new { message = $"Family with ID {id} not found." });

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _familyService.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound(new { message = $"Family with ID {id} not found." });

        return NoContent();
    }

    [HttpGet("{familyId:int}/members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyMemberDto>>> GetMembers(
        int familyId,
        CancellationToken cancellationToken)
    {
        var members = await _familyService.GetMembersAsync(familyId, cancellationToken);
        return Ok(members);
    }

    [HttpPost("{familyId:int}/members")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMemberDto>> AddMember(
        int familyId,
        [FromBody] CreateFamilyMemberRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var member = await _familyService.AddMemberAsync(
                familyId,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetMembers),
                new { familyId },
                member);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{familyId:int}/members/{memberId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember(
        int familyId,
        int memberId,
        [FromBody] UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _familyService.UpdateMemberAsync(
                familyId,
                memberId,
                request,
                cancellationToken);

            if (!updated)
            {
                return NotFound(new
                {
                    message = $"Family member with ID {memberId} was not found in family {familyId}."
                });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{familyId:int}/members/{memberId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMember(
        int familyId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var deleted = await _familyService.DeleteMemberAsync(
            familyId,
            memberId,
            cancellationToken);

        if (!deleted)
        {
            return NotFound(new
            {
                message = $"Family member with ID {memberId} was not found in family {familyId}."
            });
        }

        return NoContent();
    }
}
