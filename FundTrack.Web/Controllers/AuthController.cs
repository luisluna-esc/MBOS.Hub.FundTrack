using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;

    public AuthController(IAuthService authService, IPasswordResetService passwordResetService)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
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

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto forgotPasswordRequestDto)
    {
        var result = await _passwordResetService.RequestReset(forgotPasswordRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto resetPasswordRequestDto)
    {
        var result = await _passwordResetService.ConfirmReset(resetPasswordRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto changePasswordRequestDto)
    {
        var result = await _passwordResetService.ChangeMyPassword(changePasswordRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}
