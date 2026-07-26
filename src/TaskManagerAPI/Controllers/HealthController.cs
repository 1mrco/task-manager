using Microsoft.AspNetCore.Mvc;

namespace TaskManagerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() =>
        Ok(new { status = "healthy", service = "Task Manager API", timestamp = DateTime.UtcNow });
}
