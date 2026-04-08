using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CurrencyExchangeController : ControllerBase
{
    private readonly ILedgerService _ledger;
    private readonly IUnitOfWork _unitOfWork;

    public CurrencyExchangeController(ILedgerService ledger, IUnitOfWork unitOfWork)
    {
        _ledger = ledger;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Borsa modu: döviz alış/satış (yalnızca USD/EUR/GBP ↔ TRY). İşlem geçmişine <see cref="TransactionKind.ForexExchange"/> kaydı düşer.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ForexBorsaResponseDto>> CreateForexTrade(
        [FromBody] ForexBorsaRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.BaseCurrency is not (CashCurrency.Usd or CashCurrency.Eur or CashCurrency.Gbp))
            return BadRequest("Temel para birimi USD, EUR veya GBP olmalıdır.");
        if (dto.AmountBase <= 0 || dto.RateTryPerUnit <= 0)
            return BadRequest("Tutar ve kur sıfırdan büyük olmalıdır.");

        var counterTry = Math.Round(dto.AmountBase * dto.RateTryPerUnit, 2, MidpointRounding.AwayFromZero);
        var id = Guid.NewGuid();
        var code = dto.BaseCurrency switch
        {
            CashCurrency.Usd => "USD",
            CashCurrency.Eur => "EUR",
            CashCurrency.Gbp => "GBP",
            _ => dto.BaseCurrency.ToString()
        };
        var actionTr = dto.IsBuy ? "Alış" : "Satış";
        var desc = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Döviz işlemi: {actionTr} {dto.AmountBase:N2} {code} @ {dto.RateTryPerUnit:N4} TRY (≈ {counterTry:N2} TRY)"
            : dto.Description.Trim();

        decimal netTry;
        decimal netUsd = 0, netEur = 0, netGbp = 0;
        switch (dto.BaseCurrency)
        {
            case CashCurrency.Usd:
                netUsd = dto.IsBuy ? dto.AmountBase : -dto.AmountBase;
                break;
            case CashCurrency.Eur:
                netEur = dto.IsBuy ? dto.AmountBase : -dto.AmountBase;
                break;
            case CashCurrency.Gbp:
                netGbp = dto.IsBuy ? dto.AmountBase : -dto.AmountBase;
                break;
        }

        netTry = dto.IsBuy ? -counterTry : counterTry;

        var transaction = new Transaction
        {
            Id = id,
            TransactionDate = dto.TransactionDate,
            Kind = TransactionKind.ForexExchange,
            Direction = dto.IsBuy ? TransactionDirection.Purchase : TransactionDirection.Sale,
            Quantity = 0,
            Milyem = 0,
            TotalLabour = 0,
            MilyemLabour = 0,
            HasGram = 0,
            NetHasGram = 0,
            Price = Math.Abs(netTry),
            NetCashAmount = Math.Round(netTry, 6),
            NetCashAmountUsd = Math.Round(netUsd, 6),
            NetCashAmountEur = Math.Round(netEur, 6),
            NetCashAmountGbp = Math.Round(netGbp, 6),
            Description = desc,
            CustomerId = null,
            CorrelationId = null,
            ForexBaseCurrency = dto.BaseCurrency,
            ForexIsBuy = dto.IsBuy,
            ForexAmountBase = dto.AmountBase,
            ForexRateTryPerUnit = dto.RateTryPerUnit,
            ForexCounterTry = counterTry,
        };

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);

        await _ledger.RecordForexTradeAgainstTryAsync(
            dto.TransactionDate,
            dto.BaseCurrency,
            dto.IsBuy,
            dto.AmountBase,
            counterTry,
            id,
            desc,
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new ForexBorsaResponseDto(id));
    }

    /// <summary>Eski serbest çift döviz (deftere yalnızca yazar; işlem listesinde görünmez).</summary>
    [HttpPost("pair")]
    public async Task<ActionResult<CurrencyExchangeResponseDto>> ExchangePair(
        [FromBody] CurrencyExchangeRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.SellAmount <= 0 || dto.BuyAmount <= 0)
            return BadRequest("Tutarlar sıfırdan büyük olmalıdır.");
        if (dto.SellCurrency == dto.BuyCurrency)
            return BadRequest("Satılan ve alınan para birimleri farklı olmalıdır.");

        var referenceId = Guid.NewGuid();
        var desc = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Döviz (çift): {dto.SellAmount:N2} {dto.SellCurrency} → {dto.BuyAmount:N2} {dto.BuyCurrency}"
            : dto.Description.Trim();

        await _ledger.RecordCurrencyExchangeAsync(
            dto.TransactionDate,
            dto.SellCurrency,
            dto.SellAmount,
            dto.BuyCurrency,
            dto.BuyAmount,
            referenceId,
            desc,
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new CurrencyExchangeResponseDto(referenceId));
    }
}

public record ForexBorsaRequestDto(
    DateTime TransactionDate,
    CashCurrency BaseCurrency,
    bool IsBuy,
    decimal AmountBase,
    decimal RateTryPerUnit,
    string? Description);

public record ForexBorsaResponseDto(Guid TransactionId);

public record CurrencyExchangeRequestDto(
    DateTime TransactionDate,
    CashCurrency SellCurrency,
    decimal SellAmount,
    CashCurrency BuyCurrency,
    decimal BuyAmount,
    string? Description);

public record CurrencyExchangeResponseDto(Guid OperationId);
