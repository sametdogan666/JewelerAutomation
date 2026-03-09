using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
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

    /// <summary>
    /// Nakit bağlama simülasyonu yapar (kayıt oluşturmaz).
    /// </summary>
    [HttpPost]
    [Route("api/profit-analysis/simulate")]
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
            result.CashBalance,
            result.GoldBalance,
            result.CashEquivalentHasGram,
            result.TotalCapitalHasGram,
            result.InitialCapitalHasGram,
            result.TransactionProfitHasGram,
            result.ExchangeRateProfitHasGram,
            result.NetProfitHasGram
        ));
    }

    /// <summary>
    /// Nakit bağlama işlemini gerçekleştirir ve kaydeder.
    /// </summary>
    [HttpPost]
    [Route("api/profit-analysis/peg-cash")]
    public async Task<ActionResult<CashPeggingLogDto>> PegCash(
        [FromBody] CreatePeggingRequest request,
        CancellationToken cancellationToken)
    {
        var log = await _pegging.CreatePeggingAsync(
            request.PeriodStart,
            request.PeriodEnd,
            request.GoldPricePerGram,
            request.Notes,
            null, // TODO: Get current user ID from claims
            cancellationToken
        );

        return Ok(MapToDto(log));
    }

    /// <summary>
    /// Nakit bağlama geçmişini getirir.
    /// </summary>
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

    /// <summary>
    /// En son nakit bağlama kaydını getirir.
    /// </summary>
    [HttpGet]
    [Route("api/profit-analysis/latest-pegging")]
    public async Task<ActionResult<CashPeggingLogDto>> GetLatestPegging(CancellationToken cancellationToken)
    {
        var log = await _pegging.GetLatestPeggingAsync(cancellationToken);
        if (log == null) return NotFound();
        return Ok(MapToDto(log));
    }

    /// <summary>
    /// Dönemsel işlem özetini getirir (haftalık alış-satış detayı).
    /// </summary>
    [HttpGet]
    [Route("api/profit-analysis/period-summary")]
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
        log.TransactionProfitHasGram,
        log.ExchangeRateProfitHasGram,
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

public record PeggingSimulationDto(
    decimal CashBalance,
    decimal GoldBalance,
    decimal CashEquivalentHasGram,
    decimal TotalCapitalHasGram,
    decimal InitialCapitalHasGram,
    decimal TransactionProfitHasGram,
    decimal ExchangeRateProfitHasGram,
    decimal NetProfitHasGram
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
    decimal TransactionProfitHasGram,
    decimal ExchangeRateProfitHasGram,
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
