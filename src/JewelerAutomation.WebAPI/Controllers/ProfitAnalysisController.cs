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
    private readonly ICashPeggingService _cashPegging;
    private readonly IPeggingService _pegging;
    private readonly IAccountingService _accounting;

    public ProfitAnalysisController(
        ICashPeggingService cashPegging,
        IPeggingService pegging,
        IAccountingService accounting)
    {
        _cashPegging = cashPegging;
        _pegging = pegging;
        _accounting = accounting;
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<PeggingSimulationDto>> Simulate(
        [FromBody] SimulatePeggingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cashPegging.SimulatePeggingAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.GoldPricePerGram,
            request.PegCashFromSafe,
            request.PegHasGram,
            cancellationToken
        );

        return Ok(MapToDto(result));
    }

    [HttpPost("simulate-unified")]
    public async Task<ActionResult<UnifiedPeggingSimulationResponseDto>> SimulateUnified(
        [FromBody] UnifiedPeggingSimulateApiRequest request,
        CancellationToken cancellationToken)
    {
        var r = await _pegging.SimulateUnifiedAsync(
            new UnifiedPeggingSimulateRequest(
                request.PeriodStart,
                request.PeriodEnd,
                request.GoldPricePerGram,
                request.PegCashFromSafe,
                request.PegHasGram,
                request.FifoTargetAmountGram),
            cancellationToken);

        return Ok(new UnifiedPeggingSimulationResponseDto(
            MapToDto(r.Hybrid),
            r.Fifo == null
                ? null
                : new FifoLinkingSimulationResponseDto(
                    r.Fifo.TargetAmountGram,
                    r.Fifo.TargetPricePerGram,
                    r.Fifo.EstimatedProfitTl,
                    r.Fifo.OpenHasPositionGram,
                    r.Fifo.SufficientOpenPosition),
            r.OpenHasPositionInPeriodGram));
    }

    [HttpPost("peg-cash")]
    public async Task<ActionResult<CashPeggingLogDto>> PegCash(
        [FromBody] CreatePeggingRequest request,
        CancellationToken cancellationToken)
    {
        var log = await _pegging.CreateHybridPeggingAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.GoldPricePerGram,
            request.Notes,
            null,
            request.PegCashFromSafe,
            request.PegHasGram,
            cancellationToken);

        return Ok(MapToDto(log));
    }

    [HttpDelete("pegging/{id:guid}")]
    public async Task<ActionResult> DeletePegging(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _cashPegging.DeletePeggingAsync(id, cancellationToken);
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
            var log = await _cashPegging.UpdatePeggingAsync(
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
            logs = await _cashPegging.GetPeggingHistoryByDateRangeAsync(from.Value, to.Value, cancellationToken);
        else
            logs = await _cashPegging.GetPeggingHistoryAsync(cancellationToken);

        return Ok(logs.Select(MapToDto).ToList());
    }

    [HttpGet("latest-pegging")]
    public async Task<ActionResult<CashPeggingLogDto>> GetLatestPegging(CancellationToken cancellationToken)
    {
        var log = await _cashPegging.GetLatestPeggingAsync(cancellationToken);
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

    private static PeggingSimulationDto MapToDto(PeggingSimulationResult r) => new(
        r.PeriodCashBalance,
        r.GoldBalanceInSafe,
        r.CashEquivalentHasGram,
        r.TotalSalesHasGram,
        r.TotalPurchasesHasGram,
        r.TransactionProfitHasGram,
        r.NetProfitHasGram,
        r.NetProfitTL,
        r.SafeCashBalance,
        r.LedgerPeriodCashBalance
    );
}

public record SimulatePeggingRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GoldPricePerGram,
    decimal? PegCashFromSafe = null,
    decimal? PegHasGram = null
);

public record UnifiedPeggingSimulateApiRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GoldPricePerGram,
    decimal? PegCashFromSafe = null,
    decimal? PegHasGram = null,
    decimal? FifoTargetAmountGram = null
);

public record UnifiedPeggingSimulationResponseDto(
    PeggingSimulationDto Hybrid,
    FifoLinkingSimulationResponseDto? Fifo,
    decimal OpenHasPositionInPeriodGram);

public record FifoLinkingSimulationResponseDto(
    decimal TargetAmountGram,
    decimal TargetPricePerGram,
    decimal EstimatedProfitTl,
    decimal OpenHasPositionGram,
    bool SufficientOpenPosition);

public record CreatePeggingRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GoldPricePerGram,
    string? Notes,
    decimal? PegCashFromSafe = null,
    decimal? PegHasGram = null
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
    decimal NetProfitTL,
    decimal SafeCashBalance,
    decimal LedgerPeriodCashBalance
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
