using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;

    public TransactionsController(IUnitOfWork unitOfWork, IAccountingService accounting)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Transaction>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Transaction> list;
        if (from.HasValue && to.HasValue)
            list = await _unitOfWork.Transactions.GetByDateRangeAsync(from.Value, to.Value, cancellationToken).ConfigureAwait(false);
        else
            list = await _unitOfWork.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Transaction>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _unitOfWork.Transactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (item == null) return NotFound();
        return Ok(item);
    }

    /// <summary>
    /// Alış veya satış kaydı oluşturur. Satışta kasa altın eksilir (HasGram -), alışta kasa altın artar (HasGram +).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Transaction>> Create([FromBody] TransactionCreateDto dto, CancellationToken cancellationToken)
    {
        decimal hasGram;
        decimal totalLabour = 0;
        decimal milyemLabour;

        if (dto.Direction == TransactionDirection.Sale)
        {
            int pieces = dto.PieceCount ?? 0;
            decimal unitLabour = dto.UnitLabour ?? 0;
            totalLabour = _accounting.CalculateTotalLabour(pieces, unitLabour, subtract: true);
            hasGram = _accounting.CalculateHasGramWithLabour(dto.Quantity, dto.Milyem, totalLabour);
            milyemLabour = _accounting.CalculateMilyemLabour(dto.Quantity, dto.Milyem);
        }
        else
        {
            hasGram = _accounting.CalculateHasGram(dto.Quantity, dto.Milyem);
            milyemLabour = _accounting.CalculateMilyemLabour(dto.Quantity, dto.Milyem);
        }

        var transaction = new Transaction
        {
            TransactionDate = dto.TransactionDate,
            Direction = dto.Direction,
            Quantity = dto.Quantity,
            Milyem = dto.Milyem,
            PieceCount = dto.PieceCount,
            UnitLabour = dto.UnitLabour,
            TotalLabour = totalLabour,
            HasGram = hasGram,
            Price = dto.Price,
            Description = dto.Description,
            MilyemLabour = milyemLabour,
            CustomerId = dto.CustomerId
        };

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);

        // Kasa hareketi: Satış → altın çıkışı (HasGram negatif), Alış → altın girişi (HasGram pozitif)
        var kasaGram = dto.Direction == TransactionDirection.Sale ? -dto.Quantity : dto.Quantity;
        var kasaHasGram = dto.Direction == TransactionDirection.Sale ? -hasGram : hasGram;
        var safeMovement = new SafeMovement
        {
            TransactionDate = dto.TransactionDate,
            Gram = kasaGram,
            Milyem = dto.Milyem,
            HasGram = kasaHasGram,
            Description = dto.Direction == TransactionDirection.Sale
                ? $"Satış: {dto.Description ?? "—"}"
                : $"Alış: {dto.Description ?? "—"}",
            MovementType = dto.Direction == TransactionDirection.Sale ? SafeMovementType.Expense : SafeMovementType.Income,
            SourceTransactionId = transaction.Id
        };
        await _unitOfWork.SafeMovements.AddAsync(safeMovement, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
    }
}

public record TransactionCreateDto(
    DateTime TransactionDate,
    TransactionDirection Direction,
    decimal Quantity,
    decimal Milyem,
    int? PieceCount,
    decimal? UnitLabour,
    decimal? Price,
    string? Description,
    Guid? CustomerId);
