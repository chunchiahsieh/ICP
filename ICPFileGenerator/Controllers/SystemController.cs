using Microsoft.AspNetCore.Mvc;

namespace ICPFileGenerator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            applicationName = "ICPFileGenerator",
            status = "Running",
            version = "1.0.0"
        });
    }
}
