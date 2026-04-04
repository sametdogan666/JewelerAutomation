using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Utilities;
using JewelerAutomation.Infrastructure.GoldRates;
using JewelerAutomation.WebAPI.Hubs;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/gold-rates")]
[Route("api/GoldRates")]
public class GoldRatesController : ControllerBase
{
    private readonly IGoldRatesRepository _goldRates;
    private readonly IHubContext<GoldRatesHub> _hub;
    private readonly IMemoryCache _cache;

    public GoldRatesController(
        IGoldRatesRepository goldRates,
        IHubContext<GoldRatesHub> hub,
        IMemoryCache cache)
    {
        _goldRates = goldRates;
        _hub = hub;
        _cache = cache;
    }

    public sealed record SetManualRateRequest(decimal HasTryPerGramMid, decimal? UsdTryMid);

    /// <summary>
    /// Türkiye takvim günü için manuel HAS (TL/gr); panel anında bu değeri kullanır.
    /// </summary>
    [HttpPost("manual")]
    public async Task<IActionResult> SetManualDayRate([FromBody] SetManualRateRequest body, CancellationToken cancellationToken)
    {
        if (body.HasTryPerGramMid is <= 0 or > 100_000)
            return BadRequest("Geçersiz HAS kuru.");

        Guid? userId = null;
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(idClaim, out var g))
            userId = g;

        var day = TurkeyClock.TodayDateOnly();
        await _goldRates.UpsertManualAsync(day, body.HasTryPerGramMid, body.UsdTryMid, userId, cancellationToken).ConfigureAwait(false);

        _cache.Remove(GoldRateService.CacheKey);

        await _hub.Clients.All.SendAsync("RatesUpdated", cancellationToken: cancellationToken).ConfigureAwait(false);
        return Ok(new { effectiveDate = day.ToString("yyyy-MM-dd"), ok = true });
    }
}
