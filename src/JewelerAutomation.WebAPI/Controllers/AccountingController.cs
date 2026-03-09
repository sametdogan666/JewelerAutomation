using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Services;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accountingService;

    public AccountingController(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    /// <summary>
    /// Kuyumculuk kar/zarar hesaplama (Nakit Bağlama Mantığı).
    /// Kullanıcı has fiyatını girer, sistem net sermaye ve karı hesaplar.
    /// </summary>
    /// <param name="goldPrice">Has fiyatı (TL/gram)</param>
    /// <param name="startDate">Başlangıç tarihi (opsiyonel, filtreleme için)</param>
    /// <param name="endDate">Bitiş tarihi (opsiyonel, filtreleme için)</param>
    [HttpGet("profit")]
    public async Task<ActionResult<AccountingProfitDto>> CalculateProfit(
        [FromQuery] decimal goldPrice,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (goldPrice <= 0)
        {
            return BadRequest(new { error = "Gold price must be greater than 0." });
        }

        var result = await _accountingService.CalculateProfitAsync(goldPrice, startDate, endDate, cancellationToken);
        
        return Ok(new AccountingProfitDto(
            result.InitialCapitalHasGram,
            result.CurrentGoldInSafeHasGram,
            result.CurrentCashBalanceTL,
            result.CashEquivalentHasGram,
            result.NetCapitalHasGram,
            result.NetProfitHasGram,
            result.GoldPriceUsed
        ));
    }

    /// <summary>
    /// İlk Ana Sermaye hareketini döndürür (başlangıç sermayesi).
    /// </summary>
    [HttpGet("initial-capital")]
    public async Task<ActionResult<decimal>> GetInitialCapital(CancellationToken cancellationToken)
    {
        var capital = await _accountingService.GetInitialCapitalAsync(cancellationToken);
        return Ok(capital);
    }

    /// <summary>
    /// Transaction'lardan hesaplanan nakit bakiyesini döndürür.
    /// </summary>
    [HttpGet("cash-balance")]
    public async Task<ActionResult<CashBalanceDto>> GetCashBalance(CancellationToken cancellationToken)
    {
        var result = await _accountingService.GetCashBalanceAsync(cancellationToken);
        return Ok(new CashBalanceDto(
            result.TotalSalesCash,
            result.TotalPurchasesCash,
            result.NetCashBalance
        ));
    }
}

public record AccountingProfitDto(
    decimal InitialCapitalHasGram,
    decimal CurrentGoldInSafeHasGram,
    decimal CurrentCashBalanceTL,
    decimal CashEquivalentHasGram,
    decimal NetCapitalHasGram,
    decimal NetProfitHasGram,
    decimal GoldPriceUsed
);

public record CashBalanceDto(
    decimal TotalSalesCash,
    decimal TotalPurchasesCash,
    decimal NetCashBalance
);
