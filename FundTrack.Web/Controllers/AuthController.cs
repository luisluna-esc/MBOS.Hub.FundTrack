using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

// Sin "olvidé mi contraseña": M-BOS crea los usuarios con contraseña temporal y el usuario
// la cambia con change-password (obligatorio al primer ingreso).
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPasswordService _passwordService;

    public AuthController(IAuthService authService, IPasswordService passwordService)
    {
        _authService = authService;
        _passwordService = passwordService;
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

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto changePasswordRequestDto)
    {
        var result = await _passwordService.ChangeMyPassword(changePasswordRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}
