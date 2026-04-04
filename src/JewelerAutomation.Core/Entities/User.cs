using JewelerAutomation.Core;

namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Uygulama kullanıcısı. <see cref="Role"/> değerleri: <see cref="AppRoles.Admin"/>, <see cref="AppRoles.Staff"/>.
/// </summary>
public class User : BaseEntity
{
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>Admin veya Staff (JWT claim ile eşleşir).</summary>
    public string Role { get; set; } = AppRoles.Staff;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
