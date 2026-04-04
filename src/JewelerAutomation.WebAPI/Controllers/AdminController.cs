using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private const int MaxAuditPageSize = 500;
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <summary>Denetim kayıtları (sayfalı).</summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult<AuditLogPageDto>> GetAuditLogs(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take < 1)
            return BadRequest("Geçersiz sayfalama.");
        take = Math.Min(take, MaxAuditPageSize);

        var total = await _unitOfWork.AuditLogs.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await _unitOfWork.AuditLogs.GetRecentAsync(skip, take, cancellationToken).ConfigureAwait(false);
        var dtos = items.Select(MapAuditLog).ToList();
        return Ok(new AuditLogPageDto(dtos, total, skip, take));
    }

    /// <summary>Tüm kullanıcılar (şifre döndürülmez).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> GetUsers(CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var list = users.Select(u => new UserSummaryDto(u.Id, u.UserName, u.Role, u.IsActive, u.Email)).ToList();
        return Ok(list);
    }

    /// <summary>Yeni kullanıcı (Admin veya Staff).</summary>
    [HttpPost("users")]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(
        [FromBody] CreateApplicationUserDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Kullanıcı adı ve şifre zorunludur.");
        if (dto.Password.Length < 6)
            return BadRequest("Şifre en az 6 karakter olmalıdır.");

        var role = dto.Role?.Trim();
        if (role != AppRoles.Admin && role != AppRoles.Staff)
            return BadRequest($"Rol yalnızca '{AppRoles.Admin}' veya '{AppRoles.Staff}' olabilir.");

        var normalized = dto.UserName.Trim().ToUpperInvariant();
        if (await _unitOfWork.Users.GetByUserNameAsync(normalized, cancellationToken).ConfigureAwait(false) != null)
            return Conflict("Bu kullanıcı adı zaten kayıtlı.");

        var user = new User
        {
            UserName = dto.UserName.Trim(),
            NormalizedUserName = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role,
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            IsActive = true
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return StatusCode(StatusCodes.Status201Created,
            new UserSummaryDto(user.Id, user.UserName, user.Role, user.IsActive, user.Email));
    }

    private static AuditLogEntryDto MapAuditLog(AuditLog x)
        => new(
            x.Id,
            x.UserId,
            x.UserName,
            x.Action.ToString(),
            x.EntityName,
            x.EntityId,
            x.OldValues,
            x.NewValues,
            x.Timestamp);
}

public record AuditLogEntryDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityName,
    string EntityId,
    string? OldValues,
    string? NewValues,
    DateTime Timestamp);

public record AuditLogPageDto(
    IReadOnlyList<AuditLogEntryDto> Items,
    int TotalCount,
    int Skip,
    int Take);

public record UserSummaryDto(Guid Id, string UserName, string Role, bool IsActive, string? Email);

public record CreateApplicationUserDto(string UserName, string Password, string Role, string? Email = null);
