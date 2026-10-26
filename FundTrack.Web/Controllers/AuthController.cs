using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

// Ingreso, renovación de la sesión y salida. No hay "olvidé mi contraseña" ni cambio de contraseña
// por el usuario: la contraseña la asigna M-BOS desde Administración, Usuarios.
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var result = await _authService.Login(loginDto, HttpContext.Connection.RemoteIpAddress?.ToString());
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto refreshRequestDto)
    {
        var result = await _authService.RefreshToken(refreshRequestDto, HttpContext.Connection.RemoteIpAddress?.ToString());
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto logoutRequestDto)
    {
        var result = await _authService.Logout(logoutRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}
