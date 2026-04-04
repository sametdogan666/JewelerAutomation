using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardSummaryService _dashboard;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardSummaryService dashboard, ILogger<DashboardController> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    /// <summary>
    /// Genel özet: ham defter (Ledger) + işlem/kasa hareketleri + cari bakiyeleri; manuel kur sadece etiket.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await _dashboard.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
            return Ok(dto);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Dashboard summary timed out; returning degraded payload.");
            return Ok(DashboardSummaryDtoDefaults.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard summary failed; returning degraded payload.");
            return Ok(DashboardSummaryDtoDefaults.Empty);
        }
    }
}
