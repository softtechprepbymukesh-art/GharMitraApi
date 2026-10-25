using Microsoft.AspNetCore.Mvc;

namespace GharMitra.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    [HttpGet("/health")]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            application = "GharMitra.Api",
            version = "1.0.0"
        });
    }
}