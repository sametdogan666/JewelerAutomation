using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.WebAPI.Models.Auth;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.WebAPI.Services;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;

    public AuthController(IUnitOfWork unitOfWork, IJwtService jwtService)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Login with UserName and Password. Returns JWT token.
    /// Seed admin: UserName=admin, Password=Admin123!
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.UserName.Trim().ToUpperInvariant();
        var user = await _unitOfWork.Users.GetByUserNameAsync(normalizedName, cancellationToken).ConfigureAwait(false);
        if (user == null)
            return Unauthorized("Invalid user or password.");

        if (!user.IsActive)
            return Unauthorized("Account is disabled.");

        var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!valid)
            return Unauthorized("Invalid user or password.");

        var token = _jwtService.GenerateToken(user);
        var expiryMinutes = int.TryParse(HttpContext.RequestServices.GetService<IConfiguration>()?["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        return Ok(new LoginResponse
        {
            Token = token,
            UserName = user.UserName,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        });
    }
}
