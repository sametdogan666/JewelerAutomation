namespace JewelerAutomation.Core.Entities;

public class User : BaseEntity
{
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // Admin, User, etc.
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
