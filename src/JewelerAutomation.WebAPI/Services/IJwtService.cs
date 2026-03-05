using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Services;

public interface IJwtService
{
    string GenerateToken(User user);
}
