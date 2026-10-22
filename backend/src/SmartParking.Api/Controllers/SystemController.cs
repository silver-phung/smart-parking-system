using Microsoft.AspNetCore.Mvc;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public SystemController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Returns basic system metadata and environment information.
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSystemInfo()
    {
        return Ok(new
        {
            Service = "Smart Parking API",
            Project = "FPT Software OJT - Smart Parking System",
            Environment = _environment.EnvironmentName,
            Status = "Online",
            TimestampUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Returns lightweight application health status.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            TimestampUtc = DateTime.UtcNow
        });
    }
}
