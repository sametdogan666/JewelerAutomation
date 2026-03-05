using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SafeController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;

    public SafeController(IUnitOfWork unitOfWork, IAccountingService accounting)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
    }

    [HttpGet("balance")]
    public async Task<ActionResult<decimal>> GetBalance(CancellationToken cancellationToken)
    {
        var balance = await _unitOfWork.SafeMovements.GetTotalHasGramBalanceAsync(cancellationToken).ConfigureAwait(false);
        return Ok(balance);
    }

    [HttpGet("movements")]
    public async Task<ActionResult<IReadOnlyList<SafeMovement>>> GetMovements(CancellationToken cancellationToken)
    {
        var list = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpPost("movements")]
    public async Task<ActionResult<SafeMovement>> AddMovement([FromBody] SafeMovementCreateDto dto, CancellationToken cancellationToken)
    {
        var hasGram = _accounting.CalculateHasGram(dto.Gram, dto.Milyem);
        var entity = new SafeMovement
        {
            TransactionDate = dto.TransactionDate,
            Gram = dto.Gram,
            Milyem = dto.Milyem,
            HasGram = hasGram,
            Description = dto.Description,
            MovementType = dto.MovementType
        };
        await _unitOfWork.SafeMovements.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetMovements), null, entity);
    }
}

public record SafeMovementCreateDto(DateTime TransactionDate, decimal Gram, decimal Milyem, string? Description, SafeMovementType MovementType);
