using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfitController : ControllerBase
{
    private readonly IProfitCalculationService _profitService;

    public ProfitController(IProfitCalculationService profitService)
    {
        _profitService = profitService;
    }

    /// <summary>
    /// Belirli bir tarih aralığındaki kar hesaplaması (saf altın bazında).
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi (ISO 8601 format)</param>
    /// <param name="endDate">Bitiş tarihi (ISO 8601 format)</param>
    [HttpGet("calculate")]
    public async Task<ActionResult<ProfitSummaryDto>> Calculate(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date." });
        }

        var result = await _profitService.CalculateProfitAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);
        
        return Ok(new ProfitSummaryDto(
            result.TotalGoldSalesHas,
            result.TotalGoldPurchasesHas,
            result.NetProfitHas,
            result.StartDate,
            result.EndDate
        ));
    }
}

public record ProfitSummaryDto(
    decimal TotalGoldSalesHas,
    decimal TotalGoldPurchasesHas,
    decimal NetProfitHas,
    DateTime StartDate,
    DateTime EndDate
);
