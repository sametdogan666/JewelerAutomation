using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Services;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LinkingController : ControllerBase
{
    private readonly IGoldLinkingService _linking;

    public LinkingController(IGoldLinkingService linking)
    {
        _linking = linking;
    }

    [HttpGet("open-position")]
    public async Task<ActionResult<decimal>> GetOpenPosition(
        [FromQuery] DateTime? periodStart,
        [FromQuery] DateTime? periodEnd,
        CancellationToken cancellationToken)
    {
        var v = await _linking.GetOpenHasPositionAsync(periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
        return Ok(v);
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<FifoLinkingSimulationResult>> Simulate(
        [FromBody] LinkingRequestDto dto,
        CancellationToken cancellationToken)
    {
        var r = await _linking.SimulateFifoLinkingAsync(
                dto.TargetAmountGram,
                dto.TargetPricePerGram,
                dto.PeriodStart,
                dto.PeriodEnd,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(r);
    }

    [HttpPost("process")]
    public async Task<ActionResult<LinkingProcessResultDto>> Process(
        [FromBody] LinkingRequestDto dto,
        CancellationToken cancellationToken)
    {
        var r = await _linking.ProcessPartialLinkingAsync(
                dto.TargetAmountGram,
                dto.TargetPricePerGram,
                dto.Notes,
                dto.PeriodStart,
                dto.PeriodEnd,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(r);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<LinkingProcessListItemDto>>> History(CancellationToken cancellationToken)
    {
        var list = await _linking.GetLinkingHistoryAsync(cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _linking.CancelLinkingAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}

public record LinkingRequestDto(
    decimal TargetAmountGram,
    decimal TargetPricePerGram,
    string? Notes,
    DateTime? PeriodStart = null,
    DateTime? PeriodEnd = null);
