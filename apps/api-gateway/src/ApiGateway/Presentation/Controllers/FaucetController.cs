using Bizcord.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.ApiGateway.Controllers;

[ApiController]
[Route("[controller]")]
public class FaucetController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;

    public FaucetController(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost]
    public IActionResult Dispense([FromBody] CreateTokenOptions options)
    {
        var token = _jwtTokenService.CreateToken(options);
        return Ok(token);
    }
}
