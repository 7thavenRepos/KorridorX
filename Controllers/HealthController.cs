using KorridorX.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var data = new
        {
            service = "KorridorX API",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        };

        return Ok(ApiResponses.Ok(data, "KorridorX API is running."));
    }
}