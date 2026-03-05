using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/customers/{customerId:guid}/account")]
[Authorize]
public class CustomerAccountController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;

    public CustomerAccountController(IUnitOfWork unitOfWork, IAccountingService accounting)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
    }

    /// <summary>
    /// Get customer gold and cash balance.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<CustomerBalanceDto>> GetCustomerBalance(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        var (goldBalance, cashBalance) = await _unitOfWork.CustomerTransactions.GetBalanceAsync(customerId, cancellationToken).ConfigureAwait(false);
        return Ok(new CustomerBalanceDto(customerId, customer.Name, goldBalance, cashBalance));
    }

    /// <summary>
    /// Get customer statement (list of account transactions), optionally filtered by date range.
    /// </summary>
    [HttpGet("statement")]
    public async Task<ActionResult<IReadOnlyList<CustomerTransactionDto>>> GetCustomerStatement(
        Guid customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        var list = await _unitOfWork.CustomerTransactions.GetStatementAsync(customerId, from, to, cancellationToken).ConfigureAwait(false);
        var dtos = list.Select(t => new CustomerTransactionDto(t.Id, t.TransactionDate, t.TransactionType, t.GoldGram, t.GoldMilyem, t.GoldHas, t.CashAmount, t.Description)).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Create a new customer account transaction (gold purchase/sale, cash payment/collection).
    /// </summary>
    [HttpPost("transactions")]
    public async Task<ActionResult<CustomerTransactionDto>> CreateCustomerTransaction(Guid customerId, [FromBody] CreateCustomerTransactionRequest dto, CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        decimal goldHas = dto.GoldHas;
        if (dto.TransactionType is CustomerTransactionType.GoldPurchase or CustomerTransactionType.GoldSale)
        {
            if (dto.GoldGram > 0 && dto.GoldMilyem >= 0)
                goldHas = _accounting.CalculateHasGram(dto.GoldGram, dto.GoldMilyem);
        }

        var entity = new CustomerTransaction
        {
            CustomerId = customerId,
            TransactionDate = dto.TransactionDate,
            TransactionType = dto.TransactionType,
            GoldGram = dto.GoldGram,
            GoldMilyem = dto.GoldMilyem,
            GoldHas = goldHas,
            CashAmount = dto.CashAmount,
            Description = dto.Description
        };
        await _unitOfWork.CustomerTransactions.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetCustomerStatement), new { customerId }, new CustomerTransactionDto(entity.Id, entity.TransactionDate, entity.TransactionType, entity.GoldGram, entity.GoldMilyem, entity.GoldHas, entity.CashAmount, entity.Description));
    }
}

public record CustomerBalanceDto(Guid CustomerId, string CustomerName, decimal GoldBalance, decimal CashBalance);

public record CustomerTransactionDto(
    Guid Id,
    DateTime TransactionDate,
    CustomerTransactionType TransactionType,
    decimal GoldGram,
    decimal GoldMilyem,
    decimal GoldHas,
    decimal CashAmount,
    string? Description);

public record CreateCustomerTransactionRequest(
    DateTime TransactionDate,
    CustomerTransactionType TransactionType,
    decimal GoldGram,
    decimal GoldMilyem,
    decimal GoldHas,
    decimal CashAmount,
    string? Description);
