namespace JewelerAutomation.Core;

/// <summary>JWT <see cref="System.Security.Claims.ClaimTypes.Role"/> ve <see cref="Entities.User.Role"/> ile aynı dizeler.</summary>
public static class AppRoles
{
    public const string Admin = nameof(Admin);
    public const string Staff = nameof(Staff);
}
