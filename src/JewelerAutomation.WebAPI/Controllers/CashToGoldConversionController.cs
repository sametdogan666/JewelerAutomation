using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashToGoldConversionController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;

    public CashToGoldConversionController(IUnitOfWork unitOfWork, ILedgerService ledgerService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
    }

    /// <summary>
    /// Returns all Cari-type customers that can be selected as trading partners.
    /// </summary>
    [HttpGet("trading-partners")]
    public async Task<ActionResult<IReadOnlyList<TradingPartnerDto>>> GetTradingPartners(CancellationToken cancellationToken)
    {
        var customers = await _unitOfWork.Customers.GetByTypeAsync(CustomerType.Cari, cancellationToken, includeInactive: false);
        var dtos = customers.Select(c => new TradingPartnerDto(c.Id, c.Name)).ToList();
        return Ok(dtos);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CashToGoldConversionDto>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashToGoldConversion> list;

        if (from.HasValue && to.HasValue)
            list = await _unitOfWork.CashToGoldConversions.GetByPeriodAsync(from.Value, to.Value, cancellationToken);
        else
            list = await _unitOfWork.CashToGoldConversions.GetAllAsync(cancellationToken);

        var dtos = list.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CashToGoldConversionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var conversion = await _unitOfWork.CashToGoldConversions.GetByIdAsync(id, cancellationToken);
        if (conversion == null) return NotFound();
        return Ok(MapToDto(conversion));
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CashToGoldConversionDto>>> GetByCustomer(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken);
        if (customer == null) return NotFound();

        var list = await _unitOfWork.CashToGoldConversions.GetByCustomerAsync(customerId, cancellationToken);
        return Ok(list.Select(MapToDto).ToList());
    }

    [HttpPost("calculate")]
    public ActionResult<ConversionCalculationDto> Calculate([FromBody] ConversionCalculationRequest request)
    {
        if (request.CashAmount <= 0 || request.HasPrice <= 0)
            return BadRequest("Cash amount and Has price must be greater than zero");

        var convertedGoldHas = request.CashAmount / request.HasPrice;
        return Ok(new ConversionCalculationDto(request.CashAmount, request.HasPrice, convertedGoldHas));
    }

    [HttpPost]
    public async Task<ActionResult<CashToGoldConversionDto>> Create(
        [FromBody] CreateCashToGoldConversionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashAmount <= 0 || request.HasPrice <= 0)
            return BadRequest("Cash amount and Has price must be greater than zero");

        var customer = await _unitOfWork.Customers.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer == null)
            return BadRequest("Trading partner not found");

        if (customer.Type != CustomerType.Cari)
            return BadRequest("Selected customer is not a Cari (trading partner). Only Cari-type customers can be selected.");

        var convertedGoldHas = request.CashAmount / request.HasPrice;

        var conversion = new CashToGoldConversion
        {
            TransactionDate = request.TransactionDate,
            CashAmount = request.CashAmount,
            HasPrice = request.HasPrice,
            ConvertedGoldHas = convertedGoldHas,
            CustomerId = request.CustomerId,
            Description = request.Description
                ?? $"Nakit-Altın Dönüşümü ({customer.Name}): {request.CashAmount:N2} TL → {convertedGoldHas:N2} Has Gr"
        };

        await _unitOfWork.CashToGoldConversions.AddAsync(conversion, cancellationToken);

        await _ledgerService.RecordCashToGoldConversionAsync(
            transactionDate: conversion.TransactionDate,
            cashAmount: conversion.CashAmount,
            goldHasAmount: conversion.ConvertedGoldHas,
            referenceId: conversion.Id,
            customerId: conversion.CustomerId,
            description: conversion.Description,
            cancellationToken: cancellationToken
        );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        conversion.Customer = customer;
        return CreatedAtAction(nameof(GetById), new { id = conversion.Id }, MapToDto(conversion));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var conversion = await _unitOfWork.CashToGoldConversions.GetByIdAsync(id, cancellationToken);
        if (conversion == null) return NotFound();

        await _ledgerService.DeleteEntriesByReferenceAsync(
            LedgerReferenceType.CashToGoldConversion,
            id,
            cancellationToken
        );

        _unitOfWork.CashToGoldConversions.Remove(conversion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ConversionStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        var all = await _unitOfWork.CashToGoldConversions.GetAllAsync(cancellationToken);

        var totalCash = all.Sum(c => c.CashAmount);
        var totalGold = all.Sum(c => c.ConvertedGoldHas);
        var avgPrice = totalGold > 0 ? totalCash / totalGold : 0;

        return Ok(new ConversionStatsDto(totalCash, totalGold, avgPrice, all.Count));
    }

    private static CashToGoldConversionDto MapToDto(CashToGoldConversion c) =>
        new(c.Id, c.TransactionDate, c.CashAmount, c.HasPrice, c.ConvertedGoldHas,
            c.CustomerId, c.Customer?.Name, c.Description);
}

public record TradingPartnerDto(
    Guid Id,
    string Name
);

public record CashToGoldConversionDto(
    Guid Id,
    DateTime TransactionDate,
    decimal CashAmount,
    decimal HasPrice,
    decimal ConvertedGoldHas,
    Guid? CustomerId,
    string? CustomerName,
    string? Description
);

public record CreateCashToGoldConversionRequest(
    DateTime TransactionDate,
    decimal CashAmount,
    decimal HasPrice,
    Guid CustomerId,
    string? Description
);

public record ConversionCalculationRequest(
    decimal CashAmount,
    decimal HasPrice
);

public record ConversionCalculationDto(
    decimal CashAmount,
    decimal HasPrice,
    decimal ConvertedGoldHas
);

public record ConversionStatsDto(
    decimal TotalCashConverted,
    decimal TotalGoldReceived,
    decimal AveragePrice,
    int ConversionCount
);
