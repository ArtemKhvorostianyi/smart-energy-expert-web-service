using Microsoft.AspNetCore.Mvc;
using SmartEnergyExpert.Api.DTOs;

namespace SmartEnergyExpert.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<object> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required.");
        }

        // Placeholder response for MVP bootstrap.
        return Ok(new
        {
            accessToken = "replace-with-jwt-in-next-iteration",
            expiresInSeconds = 3600
        });
    }
}
