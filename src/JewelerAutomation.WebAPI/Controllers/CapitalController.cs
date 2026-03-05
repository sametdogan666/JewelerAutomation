using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CapitalController : ControllerBase
{
    private readonly ICapitalCalculationService _capitalService;

    public CapitalController(ICapitalCalculationService capitalService)
    {
        _capitalService = capitalService;
    }

    /// <summary>
    /// Get net capital summary: gold in safe, customer debts/receivables, net gold capital.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<CapitalSummaryDto>> GetCapitalSummary(CancellationToken cancellationToken)
    {
        var summary = await _capitalService.GetCapitalSummaryAsync(cancellationToken);
        return Ok(new CapitalSummaryDto(
            summary.TotalGoldInSafe,
            summary.TotalCashInSafe,
            summary.TotalCustomerGoldDebt,
            summary.TotalCustomerGoldReceivable,
            summary.NetGoldCapital
        ));
    }
}

public record CapitalSummaryDto(
    decimal TotalGoldInSafe,
    decimal TotalCashInSafe,
    decimal TotalCustomerGoldDebt,
    decimal TotalCustomerGoldReceivable,
    decimal NetGoldCapital
);
