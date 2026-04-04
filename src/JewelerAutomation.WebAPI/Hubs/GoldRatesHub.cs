using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JewelerAutomation.WebAPI.Hubs;

[Authorize]
public sealed class GoldRatesHub : Hub
{
}
