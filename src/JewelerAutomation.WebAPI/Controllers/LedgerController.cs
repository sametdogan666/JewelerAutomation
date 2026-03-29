using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LedgerController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILedgerMigrationService _migrationService;

    public LedgerController(IUnitOfWork unitOfWork, ILedgerService ledgerService, ILedgerMigrationService migrationService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _migrationService = migrationService;
    }

    [HttpGet("balances")]
    public async Task<ActionResult<LedgerBalances>> GetBalances(CancellationToken cancellationToken)
    {
        var balances = await _ledgerService.GetBalancesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(balances);
    }

    [HttpGet("customer/{customerId:guid}/balances")]
    public async Task<ActionResult<CustomerLedgerBalances>> GetCustomerBalances(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        var balances = await _ledgerService.GetCustomerBalancesAsync(customerId, cancellationToken).ConfigureAwait(false);
        return Ok(balances);
    }

    [HttpGet("entries")]
    public async Task<ActionResult<IReadOnlyList<LedgerEntry>>> GetEntries(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        IEnumerable<LedgerEntry> entries;
        
        if (from.HasValue && to.HasValue)
        {
            entries = await _unitOfWork.Ledger.GetByPeriodAsync(from.Value, to.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entries = await _unitOfWork.Ledger.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        return Ok(entries.ToList());
    }

    [HttpGet("customer/{customerId:guid}/entries")]
    public async Task<ActionResult<IReadOnlyList<LedgerEntry>>> GetCustomerEntries(
        Guid customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        IEnumerable<LedgerEntry> entries;

        if (from.HasValue && to.HasValue)
        {
            entries = await _unitOfWork.Ledger.GetByCustomerAndPeriodAsync(customerId, from.Value, to.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entries = await _unitOfWork.Ledger.FindAsync(e => e.CustomerId == customerId, cancellationToken).ConfigureAwait(false);
        }

        return Ok(entries.ToList());
    }

    [HttpPost("migrate")]
    public async Task<ActionResult> MigrateExistingData(CancellationToken cancellationToken)
    {
        await _migrationService.MigrateExistingDataToLedgerAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { message = "Ledger migration completed successfully" });
    }
}
