using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;
    private readonly ILedgerService _ledger;
    private readonly IGoldLinkingService _goldLinking;
    private readonly ICashPeggingService _cashPegging;

    public TransactionsController(
        AppDbContext context,
        IUnitOfWork unitOfWork,
        IAccountingService accounting,
        ILedgerService ledger,
        IGoldLinkingService goldLinking,
        ICashPeggingService cashPegging)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _accounting = accounting;
        _ledger = ledger;
        _goldLinking = goldLinking;
        _cashPegging = cashPegging;
    }

    /// <summary>Alış: Has = gr×milyem (milyem≤1 ondalık; &gt;1 binlik×0,001); adet/işçilik yok. Satış: saf has + işçilik.</summary>
    private ResolvedBasketItem ResolveBasketItem(BasketItemDto itemDto)
    {
        if (itemDto.Direction == TransactionDirection.Purchase)
        {
            var milyemLabour = _accounting.CalculateMilyemLabour(itemDto.Quantity, itemDto.Milyem);
            var hasGram = _accounting.CalculateHasGram(itemDto.Quantity, itemDto.Milyem);
            return new ResolvedBasketItem(null, null, 0m, hasGram, milyemLabour);
        }

        int rawPieces = itemDto.PieceCount ?? 0;
        var labourPieces = rawPieces < 1 ? 1 : rawPieces;
        decimal unitLabour = itemDto.UnitLabour ?? 0;
        decimal totalLabour = _accounting.CalculateTotalLabour(labourPieces, unitLabour);
        decimal hasGramSale = _accounting.CalculateHasGramWithLabour(itemDto.Quantity, itemDto.Milyem, totalLabour);
        decimal milyemLabourSale = _accounting.CalculateMilyemLabour(itemDto.Quantity, itemDto.Milyem);
        return new ResolvedBasketItem(itemDto.PieceCount, itemDto.UnitLabour, totalLabour, hasGramSale, milyemLabourSale);
    }

    /// <summary>Sepette gönderilen fiyat = Has başına TL; satır nakit = Has × birim fiyat.</summary>
    private static decimal LineCashFromUnitPrice(decimal hasGram, decimal? unitPricePerHasGram) =>
        Math.Round(hasGram * (unitPricePerHasGram ?? 0m), 6);

    /// <summary>Önce doğrudan toplam TL (hesap makinesi); yoksa Has × birim fiyat.</summary>
    private static decimal ResolveLineCash(decimal hasGram, decimal? unitPricePerHasGram, decimal? lineTotalTl)
    {
        if (lineTotalTl.HasValue)
            return Math.Round(lineTotalTl.Value, 6);
        return LineCashFromUnitPrice(hasGram, unitPricePerHasGram);
    }

    private sealed record ResolvedBasketItem(
        int? PieceCount,
        decimal? UnitLabour,
        decimal TotalLabour,
        decimal HasGram,
        decimal MilyemLabour);

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
            var r = ResolveBasketItem(itemDto);
            var lineCash = ResolveLineCash(r.HasGram, itemDto.Price, itemDto.LineTotal);

            var item = new TransactionItem
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                Direction = itemDto.Direction,
                Quantity = itemDto.Quantity,
                Milyem = itemDto.Milyem,
                PieceCount = r.PieceCount,
                UnitLabour = r.UnitLabour,
                TotalLabour = r.TotalLabour,
                HasGram = r.HasGram,
                Price = lineCash,
                Description = itemDto.Description,
                MilyemLabour = r.MilyemLabour,
                ProductTemplateId = itemDto.ProductTemplateId,
            };

            transaction.Items.Add(item);

            var itemCash = lineCash;
            if (itemDto.Direction == TransactionDirection.Purchase)
            {
                totalBuyHas += r.HasGram;
                totalBuyCash += itemCash;
            }
            else
            {
                totalSellHas += r.HasGram;
                totalSellCash += itemCash;
            }

            // SafeMovement per item
            var kasaGram = itemDto.Direction == TransactionDirection.Sale ? -itemDto.Quantity : itemDto.Quantity;
            var kasaHasGram = itemDto.Direction == TransactionDirection.Sale ? -r.HasGram : r.HasGram;
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
                goldHasAmount: r.HasGram,
                cashAmount: lineCash,
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
    /// Sepeti diferansiyel günceller: izlenen aggregate (Transaction + Items + GoldTransactions), kasa/defter kalemlerle yeniden üretilir.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, [FromBody] BasketCreateDto dto, CancellationToken cancellationToken)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Sepette en az bir kalem olmalıdır.");

        if (await _goldLinking.HasPartialLinkForTransactionAsync(id, cancellationToken).ConfigureAwait(false))
            return BadRequest("Bu işlemde kısmi FIFO nakit bağlantısı var; sepet güncellenemez.");

        var idsInDto = dto.Items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToList();
        if (idsInDto.Count != idsInDto.Distinct().Count())
            return BadRequest("Yinelenen kalem Id.");

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existingTransaction = await _context.Transactions
                .AsTracking()
                .Include(t => t.Items)
                .ThenInclude(i => i.GoldTransactions)
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
            if (existingTransaction == null)
            {
                await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return NotFound();
            }

            var dtoIdSet = idsInDto.ToHashSet();
            var correlationId = existingTransaction.CorrelationId ?? Guid.NewGuid();

            existingTransaction.TransactionDate = dto.TransactionDate;
            existingTransaction.Description = dto.Description;
            existingTransaction.CustomerId = dto.CustomerId;
            existingTransaction.CorrelationId = correlationId;
            existingTransaction.UpdatedAt = DateTime.UtcNow;

            var orphans = existingTransaction.Items.Where(i => !dtoIdSet.Contains(i.Id)).ToList();
            foreach (var orphan in orphans)
            {
                foreach (var gt in orphan.GoldTransactions.ToList())
                    _context.GoldTransactions.Remove(gt);
                existingTransaction.Items.Remove(orphan);
                _context.TransactionItems.Remove(orphan);
            }

            var byItemId = existingTransaction.Items.ToDictionary(i => i.Id);

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.Id is { } existingItemId)
                {
                    if (!byItemId.TryGetValue(existingItemId, out var entity))
                    {
                        await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return BadRequest("Bu işleme ait olmayan kalem Id gönderildi.");
                    }
                    ApplyDtoToTransactionItem(entity, itemDto);
                    SyncGoldTransactionsForItem(entity, existingTransaction.Id);
                }
                else
                {
                    var neu = CreateNewTransactionItemFromDto(itemDto, existingTransaction.Id);
                    existingTransaction.Items.Add(neu);
                    SyncGoldTransactionsForItem(neu, existingTransaction.Id);
                }
            }

            await RebuildMovementsAndLedgerForTransactionAsync(existingTransaction, dto, correlationId, cancellationToken).ConfigureAwait(false);
            RecalculateTransactionHeaderTotals(existingTransaction);

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await dbTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var saved = await _unitOfWork.Transactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (saved == null)
            return NotFound();
        return Ok(MapToDto(saved));
    }

    private void ApplyDtoToTransactionItem(TransactionItem entity, BasketItemDto d)
    {
        var r = ResolveBasketItem(d);
        var lineCash = ResolveLineCash(r.HasGram, d.Price, d.LineTotal);

        entity.Direction = d.Direction;
        entity.Quantity = d.Quantity;
        entity.Milyem = d.Milyem;
        entity.PieceCount = r.PieceCount;
        entity.UnitLabour = r.UnitLabour;
        entity.TotalLabour = r.TotalLabour;
        entity.HasGram = r.HasGram;
        entity.Price = lineCash;
        entity.Description = d.Description;
        entity.MilyemLabour = r.MilyemLabour;
        entity.ProductTemplateId = d.ProductTemplateId;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private TransactionItem CreateNewTransactionItemFromDto(BasketItemDto d, Guid transactionId)
    {
        var r = ResolveBasketItem(d);
        var lineCash = ResolveLineCash(r.HasGram, d.Price, d.LineTotal);

        return new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            Direction = d.Direction,
            Quantity = d.Quantity,
            Milyem = d.Milyem,
            PieceCount = r.PieceCount,
            UnitLabour = r.UnitLabour,
            TotalLabour = r.TotalLabour,
            HasGram = r.HasGram,
            Price = lineCash,
            Description = d.Description,
            MilyemLabour = r.MilyemLabour,
            ProductTemplateId = d.ProductTemplateId,
        };
    }

    /// <summary>
    /// Kısmi bağlama güncellemesi zaten engellendiği için RemainingGram güvenle has ile hizalanır.
    /// </summary>
    private void SyncGoldTransactionsForItem(TransactionItem item, Guid transactionId)
    {
        var existingGt = item.GoldTransactions.ToList();
        if (item.Direction == TransactionDirection.Sale && item.HasGram > 0)
        {
            var gt = existingGt.FirstOrDefault();
            if (gt == null)
            {
                gt = new GoldTransaction
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transactionId,
                    TransactionItemId = item.Id,
                    OriginalHasGram = item.HasGram,
                    RemainingGram = item.HasGram,
                    IsFullyLinked = false,
                };
                _context.GoldTransactions.Add(gt);
                item.GoldTransactions.Add(gt);
            }
            else
            {
                gt.TransactionId = transactionId;
                gt.TransactionItemId = item.Id;
                gt.OriginalHasGram = item.HasGram;
                gt.RemainingGram = item.HasGram;
            }

            foreach (var extra in existingGt.Skip(1))
            {
                _context.GoldTransactions.Remove(extra);
                item.GoldTransactions.Remove(extra);
            }
        }
        else
        {
            foreach (var gt in existingGt)
            {
                _context.GoldTransactions.Remove(gt);
                item.GoldTransactions.Remove(gt);
            }
        }
    }

    private async Task RebuildMovementsAndLedgerForTransactionAsync(
        Transaction tx,
        BasketCreateDto dto,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var movements = await _context.SafeMovements
            .Where(m => m.SourceTransactionId == tx.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var m in movements)
        {
            m.CorrelationId = null;
            _context.SafeMovements.Remove(m);
        }

        var ledgerEntries = await _context.LedgerEntries
            .Where(le => le.ReferenceType == LedgerReferenceType.Transaction && le.ReferenceId == tx.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var le in ledgerEntries)
        {
            le.CorrelationId = null;
            _context.LedgerEntries.Remove(le);
        }

        foreach (var item in tx.Items.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id))
        {
            var kasaGram = item.Direction == TransactionDirection.Sale ? -item.Quantity : item.Quantity;
            var kasaHasGram = item.Direction == TransactionDirection.Sale ? -item.HasGram : item.HasGram;
            await _context.SafeMovements.AddAsync(new SafeMovement
            {
                TransactionDate = dto.TransactionDate,
                Gram = kasaGram,
                Milyem = item.Milyem,
                HasGram = kasaHasGram,
                Description = item.Direction == TransactionDirection.Sale
                    ? $"Satış: {item.Description ?? dto.Description ?? "—"}"
                    : $"Alış: {item.Description ?? dto.Description ?? "—"}",
                MovementType = item.Direction == TransactionDirection.Sale ? SafeMovementType.Expense : SafeMovementType.Income,
                SourceTransactionId = tx.Id,
                CorrelationId = correlationId
            }, cancellationToken).ConfigureAwait(false);

            await _ledger.RecordTransactionAsync(
                transactionDate: dto.TransactionDate,
                direction: item.Direction,
                goldHasAmount: item.HasGram,
                cashAmount: item.Price,
                referenceId: tx.Id,
                customerId: dto.CustomerId,
                description: item.Description ?? dto.Description,
                correlationId: correlationId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static void RecalculateTransactionHeaderTotals(Transaction tx)
    {
        decimal totalBuyHas = 0, totalSellHas = 0, totalBuyCash = 0, totalSellCash = 0;
        foreach (var item in tx.Items)
        {
            if (item.Direction == TransactionDirection.Purchase)
            {
                totalBuyHas += item.HasGram;
                totalBuyCash += item.Price ?? 0;
            }
            else
            {
                totalSellHas += item.HasGram;
                totalSellCash += item.Price ?? 0;
            }
        }

        var netHasGram = totalBuyHas - totalSellHas;
        var netCash = totalSellCash - totalBuyCash;
        tx.NetHasGram = Math.Round(netHasGram, 6);
        tx.NetCashAmount = Math.Round(netCash, 6);
        tx.Direction = netHasGram >= 0 ? TransactionDirection.Purchase : TransactionDirection.Sale;
        tx.HasGram = Math.Round(Math.Abs(netHasGram), 6);
        tx.Price = Math.Round(Math.Abs(netCash), 6);
        tx.Quantity = 0;
        tx.Milyem = 0;
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (transaction == null) return NotFound();

        if (await _goldLinking.HasPartialLinkForTransactionAsync(id, cancellationToken).ConfigureAwait(false))
            return BadRequest("Bu işlemde kısmi FIFO nakit bağlantısı var; işlem silinemez.");

        if (transaction.CorrelationId.HasValue
            && !transaction.Items.Any()
            && transaction.CashAmount.HasValue)
        {
            await _cashPegging.RestoreHybridPeggingFifoAsync(transaction.CorrelationId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

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
            CashAmount: tx.CashAmount,
            EquivalentHasGram: tx.EquivalentHasGram,
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
                MilyemLabour: i.MilyemLabour,
                ProductTemplateId: i.ProductTemplateId
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
    Guid? Id,
    TransactionDirection Direction,
    decimal Quantity,
    decimal Milyem,
    int? PieceCount,
    decimal? UnitLabour,
    decimal? Price,
    decimal? LineTotal,
    string? Description,
    Guid? ProductTemplateId
);

public record TransactionDto(
    Guid Id,
    DateTime TransactionDate,
    TransactionDirection Direction,
    decimal NetHasGram,
    decimal NetCashAmount,
    decimal HasGram,
    decimal? Price,
    decimal? CashAmount,
    decimal? EquivalentHasGram,
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
    decimal MilyemLabour,
    Guid? ProductTemplateId
);
