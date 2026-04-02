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
    private readonly ISafeStatusService _safeStatus;
    private readonly ILedgerService _ledger;

    public SafeController(
        IUnitOfWork unitOfWork,
        IAccountingService accounting,
        ISafeStatusService safeStatus,
        ILedgerService ledger)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
        _safeStatus = safeStatus;
        _ledger = ledger;
    }

    [HttpGet("balance")]
    public async Task<ActionResult<decimal>> GetBalance(CancellationToken cancellationToken)
    {
        var balance = await _unitOfWork.SafeMovements.GetTotalHasGramBalanceAsync(cancellationToken).ConfigureAwait(false);
        return Ok(balance);
    }

    /// <summary>
    /// Sadece manuel eklenen kasa hareketlerini döndür (alış-satıştan otomatik oluşanlar gösterilmez).
    /// </summary>
    [HttpGet("movements")]
    public async Task<ActionResult<IReadOnlyList<SafeMovement>>> GetMovements(CancellationToken cancellationToken)
    {
        var list = await _unitOfWork.SafeMovements.GetManualMovementsAsync(cancellationToken).ConfigureAwait(false);
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

        await _ledger.RecordSafeMovementAsync(
            transactionDate: dto.TransactionDate,
            movementType: dto.MovementType,
            goldHasAmount: hasGram,
            referenceId: entity.Id,
            description: dto.Description,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetMovements), null, entity);
    }

    /// <summary>
    /// Kasa durumu: Altın bakiyesi, nakit bakiyesi ve altın açığı.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SafeStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var s = await _safeStatus.GetSafeStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new SafeStatusDto(
            s.PhysicalGoldBalance,
            s.PhysicalCashBalance,
            s.ExpectedGold,
            s.GoldGapOrSurplus,
            s.CustomerGoldDebt,
            s.CustomerGoldReceivable,
            s.PersonalGoldDebt,
            s.PersonalGoldReceivable,
            s.NetGoldPosition,
            s.NetCashPosition,
            s.ProfitHasGram,
            s.CumulativePeggingProfitHasGram,
            s.PeggingCount
        ));
    }

    /// <summary>
    /// Kasa hareketini güncelle.
    /// </summary>
    [HttpPut("movements/{id:guid}")]
    public async Task<ActionResult<SafeMovement>> UpdateMovement(Guid id, [FromBody] SafeMovementCreateDto dto, CancellationToken cancellationToken)
    {
        var movement = await _unitOfWork.SafeMovements.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (movement == null) return NotFound();

        if (movement.SourceTransactionId != null)
            return BadRequest("Transaction'dan oluşan hareketler düzenlenemez.");
        if (movement.CorrelationId != null)
            return BadRequest("Nakit bağlama ile oluşturulan hareketler düzenlenemez. Bağlama kaydını güncelleyin.");
        if (movement.MovementType == SafeMovementType.LinkingProfit)
            return BadRequest("FIFO nakit bağlama kârı hareketleri yalnızca Bağlantı Geçmişi ekranından iptal edilebilir.");
        if (movement.MovementType == SafeMovementType.ProfitRealization && movement.CorrelationId != null)
            return BadRequest("Bağlama ile oluşan kâr gerçekleştirme hareketleri yalnızca bağlama iptali ile kaldırılabilir.");

        var hasGram = _accounting.CalculateHasGram(dto.Gram, dto.Milyem);
        movement.TransactionDate = dto.TransactionDate;
        movement.Gram = dto.Gram;
        movement.Milyem = dto.Milyem;
        movement.HasGram = hasGram;
        movement.Description = dto.Description;
        movement.MovementType = dto.MovementType;

        _unitOfWork.SafeMovements.Update(movement);

        await _ledger.DeleteEntriesByReferenceAsync(
            LedgerReferenceType.SafeMovement, id, cancellationToken
        ).ConfigureAwait(false);

        await _ledger.RecordSafeMovementAsync(
            transactionDate: dto.TransactionDate,
            movementType: dto.MovementType,
            goldHasAmount: hasGram,
            referenceId: id,
            description: dto.Description,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(movement);
    }

    /// <summary>
    /// Kasa hareketini sil.
    /// </summary>
    [HttpDelete("movements/{id:guid}")]
    public async Task<ActionResult> DeleteMovement(Guid id, CancellationToken cancellationToken)
    {
        var movement = await _unitOfWork.SafeMovements.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (movement == null) return NotFound();

        if (movement.SourceTransactionId != null)
            return BadRequest("Transaction'dan oluşan hareketler silinemez. Önce transaction'ı siliniz.");
        if (movement.CorrelationId != null)
            return BadRequest("Nakit bağlama ile oluşturulan hareketler silinemez. Bağlama kaydını silin.");
        if (movement.MovementType == SafeMovementType.LinkingProfit)
            return BadRequest("FIFO nakit bağlama kârı hareketleri yalnızca Bağlantı Geçmişi ekranından silinebilir.");
        if (movement.MovementType == SafeMovementType.ProfitRealization && movement.CorrelationId != null)
            return BadRequest("Bağlama ile oluşan kâr gerçekleştirme hareketleri yalnızca bağlama iptali ile silinebilir.");

        await _ledger.DeleteEntriesByReferenceAsync(
            LedgerReferenceType.SafeMovement, id, cancellationToken
        ).ConfigureAwait(false);

        _unitOfWork.SafeMovements.Delete(movement);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NoContent();
    }
}

public record SafeMovementCreateDto(DateTime TransactionDate, decimal Gram, decimal Milyem, string? Description, SafeMovementType MovementType);

public record SafeStatusDto(
    decimal PhysicalGoldBalance,
    decimal PhysicalCashBalance,
    decimal ExpectedGold,
    decimal GoldGapOrSurplus,
    decimal CustomerGoldDebt,
    decimal CustomerGoldReceivable,
    decimal PersonalGoldDebt,
    decimal PersonalGoldReceivable,
    decimal NetGoldPosition,
    decimal NetCashPosition,
    decimal ProfitHasGram,
    decimal CumulativePeggingProfitHasGram,
    int PeggingCount
);
