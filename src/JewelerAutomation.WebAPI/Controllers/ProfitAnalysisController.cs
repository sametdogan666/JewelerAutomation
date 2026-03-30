using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/profit-analysis")]
[Authorize]
public class ProfitAnalysisController : ControllerBase
{
    private readonly ICashPeggingService _pegging;
    private readonly IAccountingService _accounting;

    public ProfitAnalysisController(ICashPeggingService pegging, IAccountingService accounting)
    {
        _pegging = pegging;
        _accounting = accounting;
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<PeggingSimulationDto>> Simulate(
        [FromBody] SimulatePeggingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _pegging.SimulatePeggingAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.GoldPricePerGram,
            cancellationToken
        );

        return Ok(new PeggingSimulationDto(
            result.PeriodCashBalance,
            result.GoldBalanceInSafe,
            result.CashEquivalentHasGram,
            result.TotalSalesHasGram,
            result.TotalPurchasesHasGram,
            result.TransactionProfitHasGram,
            result.NetProfitHasGram,
            result.NetProfitTL
        ));
    }

    [HttpPost("peg-cash")]
    public async Task<ActionResult<CashPeggingLogDto>> PegCash(
        [FromBody] CreatePeggingRequest request,
        CancellationToken cancellationToken)
    {
        var log = await _pegging.CreatePeggingAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.GoldPricePerGram,
            request.Notes,
            null,
            cancellationToken
        );

        return Ok(MapToDto(log));
    }

    [HttpDelete("pegging/{id:guid}")]
    public async Task<ActionResult> DeletePegging(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _pegging.DeletePeggingAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("pegging/{id:guid}")]
    public async Task<ActionResult<CashPeggingLogDto>> UpdatePegging(
        Guid id,
        [FromBody] UpdatePeggingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _pegging.UpdatePeggingAsync(
                id,
                request.GoldPricePerGram,
                request.Notes,
                cancellationToken
            );
            return Ok(MapToDto(log));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("pegging-history")]
    public async Task<ActionResult<IReadOnlyList<CashPeggingLogDto>>> GetPeggingHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashPeggingLog> logs;

        if (from.HasValue && to.HasValue)
            logs = await _pegging.GetPeggingHistoryByDateRangeAsync(from.Value, to.Value, cancellationToken);
        else
            logs = await _pegging.GetPeggingHistoryAsync(cancellationToken);

        return Ok(logs.Select(MapToDto).ToList());
    }

    [HttpGet("latest-pegging")]
    public async Task<ActionResult<CashPeggingLogDto>> GetLatestPegging(CancellationToken cancellationToken)
    {
        var log = await _pegging.GetLatestPeggingAsync(cancellationToken);
        if (log == null) return NotFound();
        return Ok(MapToDto(log));
    }

    [HttpGet("period-summary")]
    public async Task<ActionResult<PeriodSummaryDto>> GetPeriodSummary(
        [FromQuery] DateTime periodStart,
        [FromQuery] DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var summary = await _accounting.GetPeriodTransactionSummaryAsync(periodStart, periodEnd, cancellationToken);

        return Ok(new PeriodSummaryDto(
            summary.PeriodStart,
            summary.PeriodEnd,
            summary.Transactions.Select(t => new TransactionDetailDto(
                t.Id,
                t.Date,
                t.Direction,
                t.Quantity,
                t.Milyem,
                t.HasGram,
                t.Price,
                t.CashImpact,
                t.CustomerName,
                t.Description
            )).ToList(),
            summary.TotalPurchasesHasGram,
            summary.TotalSalesHasGram,
            summary.TotalPurchasesCash,
            summary.TotalSalesCash,
            summary.NetCashChange,
            summary.NetGoldChange
        ));
    }

    private static CashPeggingLogDto MapToDto(CashPeggingLog log) => new(
        log.Id,
        log.PeggingDate,
        log.CashAmount,
        log.GoldPricePerGram,
        log.EquivalentHasGram,
        log.PhysicalGoldAtTime,
        log.TotalCapitalHasGram,
        log.PeriodStartDate,
        log.PeriodEndDate,
        log.NetProfitHasGram,
        log.Notes
    );
}

public record SimulatePeggingRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GoldPricePerGram
);

public record CreatePeggingRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GoldPricePerGram,
    string? Notes
);

public record UpdatePeggingRequest(
    decimal GoldPricePerGram,
    string? Notes
);

public record PeggingSimulationDto(
    decimal PeriodCashBalance,
    decimal GoldBalanceInSafe,
    decimal CashEquivalentHasGram,
    decimal TotalSalesHasGram,
    decimal TotalPurchasesHasGram,
    decimal TransactionProfitHasGram,
    decimal NetProfitHasGram,
    decimal NetProfitTL
);

public record CashPeggingLogDto(
    Guid Id,
    DateTime PeggingDate,
    decimal CashAmount,
    decimal GoldPricePerGram,
    decimal EquivalentHasGram,
    decimal PhysicalGoldAtTime,
    decimal TotalCapitalHasGram,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    decimal NetProfitHasGram,
    string? Notes
);

public record PeriodSummaryDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    IReadOnlyList<TransactionDetailDto> Transactions,
    decimal TotalPurchasesHasGram,
    decimal TotalSalesHasGram,
    decimal TotalPurchasesCash,
    decimal TotalSalesCash,
    decimal NetCashChange,
    decimal NetGoldChange
);

public record TransactionDetailDto(
    Guid Id,
    DateTime Date,
    string Direction,
    decimal Quantity,
    decimal Milyem,
    decimal HasGram,
    decimal Price,
    decimal CashImpact,
    string? CustomerName,
    string? Description
);
