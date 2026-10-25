using GharMitra.Application.Auth.DTOs;
using GharMitra.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Controllers;

/// <summary>Stage 0 onboarding and session management.</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Registers a new family with its Family Head. Creates the tenant.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.RegisterFamilyAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Adds a member with a login to the caller's family. Family Head only.</summary>
    [HttpPost("members")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteMember(
        [FromBody] InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.InviteMemberAsync(request, cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.LoginAsync(request, cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.RefreshAsync(request, cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
