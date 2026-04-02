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
    private readonly ILedgerService _ledger;
    private readonly IGoldLinkingService _goldLinking;

    public TransactionsController(
        IUnitOfWork unitOfWork,
        IAccountingService accounting,
        ILedgerService ledger,
        IGoldLinkingService goldLinking)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
        _ledger = ledger;
        _goldLinking = goldLinking;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Transaction> list;
        if (from.HasValue && to.HasValue)
            list = await _unitOfWork.Transactions.GetByDateRangeAsync(from.Value, to.Value, cancellationToken).ConfigureAwait(false);
        else
            list = await _unitOfWork.Transactions.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var dtos = list.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _unitOfWork.Transactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (item == null) return NotFound();
        return Ok(MapToDto(item));
    }

    /// <summary>
    /// Sepet/fatura oluşturur. Birden fazla alış-satış kalemi tek atomik işlemde kaydedilir.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create([FromBody] BasketCreateDto dto, CancellationToken cancellationToken)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Sepette en az bir kalem olmalıdır.");

        var correlationId = Guid.NewGuid();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = dto.TransactionDate,
            Description = dto.Description,
            CustomerId = dto.CustomerId,
            CorrelationId = correlationId,
            Quantity = 0,
            Milyem = 0,
            TotalLabour = 0,
            MilyemLabour = 0,
        };

        decimal totalBuyHas = 0;
        decimal totalSellHas = 0;
        decimal totalBuyCash = 0;
        decimal totalSellCash = 0;

        foreach (var itemDto in dto.Items)
        {
            decimal hasGram;
            decimal totalLabour = 0;
            decimal milyemLabour;

            if (itemDto.Direction == TransactionDirection.Sale)
            {
                int pieces = itemDto.PieceCount ?? 0;
                decimal unitLabour = itemDto.UnitLabour ?? 0;
                totalLabour = _accounting.CalculateTotalLabour(pieces, unitLabour, subtract: true);
                hasGram = _accounting.CalculateHasGramWithLabour(itemDto.Quantity, itemDto.Milyem, totalLabour);
                milyemLabour = _accounting.CalculateMilyemLabour(itemDto.Quantity, itemDto.Milyem);
            }
            else
            {
                hasGram = _accounting.CalculateHasGram(itemDto.Quantity, itemDto.Milyem);
                milyemLabour = _accounting.CalculateMilyemLabour(itemDto.Quantity, itemDto.Milyem);
            }

            var item = new TransactionItem
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                Direction = itemDto.Direction,
                Quantity = itemDto.Quantity,
                Milyem = itemDto.Milyem,
                PieceCount = itemDto.PieceCount,
                UnitLabour = itemDto.UnitLabour,
                TotalLabour = totalLabour,
                HasGram = hasGram,
                Price = itemDto.Price,
                Description = itemDto.Description,
                MilyemLabour = milyemLabour,
            };

            transaction.Items.Add(item);

            var itemCash = itemDto.Price ?? 0;
            if (itemDto.Direction == TransactionDirection.Purchase)
            {
                totalBuyHas += hasGram;
                totalBuyCash += itemCash;
            }
            else
            {
                totalSellHas += hasGram;
                totalSellCash += itemCash;
            }

            // SafeMovement per item
            var kasaGram = itemDto.Direction == TransactionDirection.Sale ? -itemDto.Quantity : itemDto.Quantity;
            var kasaHasGram = itemDto.Direction == TransactionDirection.Sale ? -hasGram : hasGram;
            var safeMovement = new SafeMovement
            {
                TransactionDate = dto.TransactionDate,
                Gram = kasaGram,
                Milyem = itemDto.Milyem,
                HasGram = kasaHasGram,
                Description = itemDto.Direction == TransactionDirection.Sale
                    ? $"Satış: {itemDto.Description ?? dto.Description ?? "—"}"
                    : $"Alış: {itemDto.Description ?? dto.Description ?? "—"}",
                MovementType = itemDto.Direction == TransactionDirection.Sale ? SafeMovementType.Expense : SafeMovementType.Income,
                SourceTransactionId = transaction.Id,
                CorrelationId = correlationId
            };
            await _unitOfWork.SafeMovements.AddAsync(safeMovement, cancellationToken).ConfigureAwait(false);

            // Ledger entries per item
            await _ledger.RecordTransactionAsync(
                transactionDate: dto.TransactionDate,
                direction: itemDto.Direction,
                goldHasAmount: hasGram,
                cashAmount: itemDto.Price,
                referenceId: transaction.Id,
                customerId: dto.CustomerId,
                description: itemDto.Description ?? dto.Description,
                correlationId: correlationId,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        // Net values: positive = gold/cash inflow
        var netHasGram = totalBuyHas - totalSellHas;
        var netCash = totalSellCash - totalBuyCash;
        transaction.NetHasGram = Math.Round(netHasGram, 6);
        transaction.NetCashAmount = Math.Round(netCash, 6);

        // Backward-compat header fields
        transaction.Direction = netHasGram >= 0 ? TransactionDirection.Purchase : TransactionDirection.Sale;
        transaction.HasGram = Math.Round(Math.Abs(netHasGram), 6);
        transaction.Price = Math.Round(Math.Abs(netCash), 6);

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var saved = await _unitOfWork.Transactions.GetByIdAsync(transaction.Id, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, MapToDto(saved!));
    }

    /// <summary>
    /// Sepeti güncelle — tüm kalemleri siler ve yeniden oluşturur.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, [FromBody] BasketCreateDto dto, CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (transaction == null) return NotFound();

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Sepette en az bir kalem olmalıdır.");

        if (await _goldLinking.HasPartialLinkForTransactionAsync(id, cancellationToken).ConfigureAwait(false))
            return BadRequest("Bu işlemde kısmi FIFO nakit bağlantısı var; sepet güncellenemez.");

        await _goldLinking.RemoveGoldTransactionsForTransactionAsync(id, cancellationToken).ConfigureAwait(false);

        // Remove old items
        if (transaction.Items.Any())
            _unitOfWork.Transactions.RemoveItems(transaction.Items.ToList());

        // Remove old safe movements
        var safeMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var relatedMovements = safeMovements.Where(m => m.SourceTransactionId == id).ToList();
        foreach (var m in relatedMovements)
            _unitOfWork.SafeMovements.Delete(m);

        // Remove old ledger entries
        await _ledger.DeleteEntriesByReferenceAsync(LedgerReferenceType.Transaction, id, cancellationToken).ConfigureAwait(false);

        var correlationId = transaction.CorrelationId ?? Guid.NewGuid();

        transaction.TransactionDate = dto.TransactionDate;
        transaction.Description = dto.Description;
        transaction.CustomerId = dto.CustomerId;
        transaction.CorrelationId = correlationId;
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.Items.Clear();

        decimal totalBuyHas = 0, totalSellHas = 0;
        decimal totalBuyCash = 0, totalSellCash = 0;

        foreach (var itemDto in dto.Items)
        {
            decimal hasGram;
            decimal totalLabour = 0;
            decimal milyemLabour;

            if (itemDto.Direction == TransactionDirection.Sale)
            {
                int pieces = itemDto.PieceCount ?? 0;
                decimal unitLabour = itemDto.UnitLabour ?? 0;
                totalLabour = _accounting.CalculateTotalLabour(pieces, unitLabour, subtract: true);
                hasGram = _accounting.CalculateHasGramWithLabour(itemDto.Quantity, itemDto.Milyem, totalLabour);
                milyemLabour = _accounting.CalculateMilyemLabour(itemDto.Quantity, itemDto.Milyem);
            }
            else
            {
                hasGram = _accounting.CalculateHasGram(itemDto.Quantity, itemDto.Milyem);
                milyemLabour = _accounting.CalculateMilyemLabour(itemDto.Quantity, itemDto.Milyem);
            }

            var item = new TransactionItem
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                Direction = itemDto.Direction,
                Quantity = itemDto.Quantity,
                Milyem = itemDto.Milyem,
                PieceCount = itemDto.PieceCount,
                UnitLabour = itemDto.UnitLabour,
                TotalLabour = totalLabour,
                HasGram = hasGram,
                Price = itemDto.Price,
                Description = itemDto.Description,
                MilyemLabour = milyemLabour,
            };
            transaction.Items.Add(item);

            var itemCash = itemDto.Price ?? 0;
            if (itemDto.Direction == TransactionDirection.Purchase)
            {
                totalBuyHas += hasGram;
                totalBuyCash += itemCash;
            }
            else
            {
                totalSellHas += hasGram;
                totalSellCash += itemCash;
            }

            var kasaGram = itemDto.Direction == TransactionDirection.Sale ? -itemDto.Quantity : itemDto.Quantity;
            var kasaHasGram = itemDto.Direction == TransactionDirection.Sale ? -hasGram : hasGram;
            var sm = new SafeMovement
            {
                TransactionDate = dto.TransactionDate,
                Gram = kasaGram,
                Milyem = itemDto.Milyem,
                HasGram = kasaHasGram,
                Description = itemDto.Direction == TransactionDirection.Sale
                    ? $"Satış: {itemDto.Description ?? dto.Description ?? "—"}"
                    : $"Alış: {itemDto.Description ?? dto.Description ?? "—"}",
                MovementType = itemDto.Direction == TransactionDirection.Sale ? SafeMovementType.Expense : SafeMovementType.Income,
                SourceTransactionId = transaction.Id,
                CorrelationId = correlationId
            };
            await _unitOfWork.SafeMovements.AddAsync(sm, cancellationToken).ConfigureAwait(false);

            await _ledger.RecordTransactionAsync(
                transactionDate: dto.TransactionDate,
                direction: itemDto.Direction,
                goldHasAmount: hasGram,
                cashAmount: itemDto.Price,
                referenceId: transaction.Id,
                customerId: dto.CustomerId,
                description: itemDto.Description ?? dto.Description,
                correlationId: correlationId,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        var netHasGram = totalBuyHas - totalSellHas;
        var netCash = totalSellCash - totalBuyCash;
        transaction.NetHasGram = Math.Round(netHasGram, 6);
        transaction.NetCashAmount = Math.Round(netCash, 6);
        transaction.Direction = netHasGram >= 0 ? TransactionDirection.Purchase : TransactionDirection.Sale;
        transaction.HasGram = Math.Round(Math.Abs(netHasGram), 6);
        transaction.Price = Math.Round(Math.Abs(netCash), 6);
        transaction.Quantity = 0;
        transaction.Milyem = 0;

        _unitOfWork.Transactions.Update(transaction);
        await _goldLinking.RegisterSaleGoldPositionsAsync(transaction, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var saved = await _unitOfWork.Transactions.GetByIdAsync(transaction.Id, cancellationToken).ConfigureAwait(false);
        return Ok(MapToDto(saved!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (transaction == null) return NotFound();

        if (await _goldLinking.HasPartialLinkForTransactionAsync(id, cancellationToken).ConfigureAwait(false))
            return BadRequest("Bu işlemde kısmi FIFO nakit bağlantısı var; işlem silinemez.");

        await _goldLinking.RemoveGoldTransactionsForTransactionAsync(id, cancellationToken).ConfigureAwait(false);

        // Find SafeMovements by SourceTransactionId
        var allMovements = await _unitOfWork.SafeMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var relatedBySource = allMovements.Where(m => m.SourceTransactionId == id).ToList();
        foreach (var m in relatedBySource)
            _unitOfWork.SafeMovements.Delete(m);

        // Also find SafeMovements by CorrelationId (catches ProfitRealization entries)
        if (transaction.CorrelationId.HasValue)
        {
            var relatedByCorrelation = await _unitOfWork.SafeMovements
                .FindByCorrelationIdAsync(transaction.CorrelationId.Value, cancellationToken).ConfigureAwait(false);
            foreach (var m in relatedByCorrelation)
            {
                if (relatedBySource.All(r => r.Id != m.Id))
                    _unitOfWork.SafeMovements.Delete(m);
            }

            await _ledger.DeleteEntriesByCorrelationAsync(transaction.CorrelationId.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _ledger.DeleteEntriesByReferenceAsync(LedgerReferenceType.Transaction, id, cancellationToken).ConfigureAwait(false);
        }

        _unitOfWork.Transactions.Delete(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    // ── DTO mapping ──

    private static TransactionDto MapToDto(Transaction tx)
    {
        return new TransactionDto(
            Id: tx.Id,
            TransactionDate: tx.TransactionDate,
            Direction: tx.Direction,
            NetHasGram: tx.NetHasGram,
            NetCashAmount: tx.NetCashAmount,
            HasGram: tx.HasGram,
            Price: tx.Price,
            Description: tx.Description,
            CustomerId: tx.CustomerId,
            CustomerName: tx.Customer?.Name,
            CorrelationId: tx.CorrelationId,
            CreatedAt: tx.CreatedAt,
            Items: tx.Items.Select(i => new TransactionItemDto(
                Id: i.Id,
                Direction: i.Direction,
                Quantity: i.Quantity,
                Milyem: i.Milyem,
                PieceCount: i.PieceCount,
                UnitLabour: i.UnitLabour,
                TotalLabour: i.TotalLabour,
                HasGram: i.HasGram,
                Price: i.Price,
                Description: i.Description,
                MilyemLabour: i.MilyemLabour
            )).ToList()
        );
    }
}

// ── Request / Response DTOs ──

public record BasketCreateDto(
    DateTime TransactionDate,
    string? Description,
    Guid? CustomerId,
    List<BasketItemDto> Items
);

public record BasketItemDto(
    TransactionDirection Direction,
    decimal Quantity,
    decimal Milyem,
    int? PieceCount,
    decimal? UnitLabour,
    decimal? Price,
    string? Description
);

public record TransactionDto(
    Guid Id,
    DateTime TransactionDate,
    TransactionDirection Direction,
    decimal NetHasGram,
    decimal NetCashAmount,
    decimal HasGram,
    decimal? Price,
    string? Description,
    Guid? CustomerId,
    string? CustomerName,
    Guid? CorrelationId,
    DateTime CreatedAt,
    List<TransactionItemDto> Items
);

public record TransactionItemDto(
    Guid Id,
    TransactionDirection Direction,
    decimal Quantity,
    decimal Milyem,
    int? PieceCount,
    decimal? UnitLabour,
    decimal TotalLabour,
    decimal HasGram,
    decimal? Price,
    string? Description,
    decimal MilyemLabour
);
