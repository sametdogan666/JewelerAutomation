using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Utilities;
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

    private static CashCurrency ParsePaymentCurrency(int? v) =>
        v switch
        {
            1 => CashCurrency.Usd,
            2 => CashCurrency.Eur,
            3 => CashCurrency.Gbp,
            _ => CashCurrency.Try
        };

    private sealed record ResolvedBasketItem(
        int? PieceCount,
        decimal? UnitLabour,
        decimal TotalLabour,
        decimal HasGram,
        decimal MilyemLabour);

    private static string? ValidateSahisBasket(BasketCreateDto dto, Customer? customer)
    {
        if (!dto.IsSahisEmanet)
            return null;
        if (dto.CustomerId == null || customer == null || customer.Type != CustomerType.Sahis)
            return "Emanet sepeti yalnız şahıs cari seçildiğinde kullanılabilir.";
        var mode = (SahisEmanetMode)dto.SahisEmanetMode;
        if (mode != SahisEmanetMode.EmanetSatis && mode != SahisEmanetMode.EmanetAlis)
            return "Emanet modu satış veya alış olarak seçilmelidir.";
        if (mode == SahisEmanetMode.EmanetSatis && dto.Items!.Any(i => i.Direction != TransactionDirection.Sale))
            return "Emanet satış sepetinde yalnız satış kalemleri olabilir.";
        if (mode == SahisEmanetMode.EmanetAlis && dto.Items!.Any(i => i.Direction != TransactionDirection.Purchase))
            return "Emanet alış sepetinde yalnız alış kalemleri olabilir.";
        return null;
    }

    private static bool SkipGoldLinkingForSale(BasketCreateDto dto, TransactionDirection direction) =>
        dto.IsSahisEmanet
        && (SahisEmanetMode)dto.SahisEmanetMode == SahisEmanetMode.EmanetSatis
        && direction == TransactionDirection.Sale;

    private async Task PostBasketItemPhysicalAsync(
        Transaction transaction,
        BasketCreateDto dto,
        BasketItemDto itemDto,
        ResolvedBasketItem r,
        decimal lineCash,
        CashCurrency payCur,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (!dto.KasaHareketli)
            return;

        var mode = (SahisEmanetMode)dto.SahisEmanetMode;
        var emanetSatis = dto.IsSahisEmanet && mode == SahisEmanetMode.EmanetSatis && itemDto.Direction == TransactionDirection.Sale;
        var emanetAlis = dto.IsSahisEmanet && mode == SahisEmanetMode.EmanetAlis && itemDto.Direction == TransactionDirection.Purchase;

        if (emanetSatis)
        {
            if (lineCash > 0)
            {
                await _ledger.RecordShopCashInAsync(
                    dto.TransactionDate,
                    lineCash,
                    payCur,
                    transaction.Id,
                    itemDto.Description ?? dto.Description,
                    correlationId,
                    cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        if (emanetAlis)
        {
            var safeMovement = new SafeMovement
            {
                TransactionDate = dto.TransactionDate,
                Gram = itemDto.Quantity,
                Milyem = itemDto.Milyem,
                HasGram = r.HasGram,
                Description = $"Emanet alış: {itemDto.Description ?? dto.Description ?? "—"}",
                MovementType = SafeMovementType.Income,
                SourceTransactionId = transaction.Id,
                CorrelationId = correlationId
            };
            await _unitOfWork.SafeMovements.AddAsync(safeMovement, cancellationToken).ConfigureAwait(false);
            await _ledger.RecordShopGoldInAsync(
                dto.TransactionDate,
                r.HasGram,
                transaction.Id,
                itemDto.Description ?? dto.Description,
                correlationId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var kasaGram = itemDto.Direction == TransactionDirection.Sale ? -itemDto.Quantity : itemDto.Quantity;
        var kasaHasGram = itemDto.Direction == TransactionDirection.Sale ? -r.HasGram : r.HasGram;
        var safeMovementStd = new SafeMovement
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
        await _unitOfWork.SafeMovements.AddAsync(safeMovementStd, cancellationToken).ConfigureAwait(false);

        await _ledger.RecordTransactionAsync(
            transactionDate: dto.TransactionDate,
            direction: itemDto.Direction,
            goldHasAmount: r.HasGram,
            cashAmount: lineCash,
            referenceId: transaction.Id,
            customerId: dto.CustomerId,
            description: itemDto.Description ?? dto.Description,
            correlationId: correlationId,
            cashCurrency: payCur,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendSahisEmanetLiabilityRowsAsync(
        Transaction tx,
        BasketCreateDto dto,
        Customer? customer,
        CancellationToken cancellationToken)
    {
        if (!tx.IsSahisEmanet || customer?.Type != CustomerType.Sahis || dto.CustomerId == null)
            return;
        var desc = tx.SahisEmanetMode == SahisEmanetMode.EmanetSatis ? "Emanet satış (sepet)" : "Emanet alış (sepet)";
        foreach (var item in tx.Items)
        {
            await _unitOfWork.CustomerTransactions.AddAsync(
                new CustomerTransaction
                {
                    Id = Guid.NewGuid(),
                    CustomerId = dto.CustomerId!.Value,
                    TransactionDate = dto.TransactionDate,
                    TransactionType = CustomerTransactionType.SahisEmanetLiability,
                    GoldGram = 0,
                    GoldMilyem = 0,
                    GoldHas = item.HasGram,
                    CashAmount = 0,
                    CashCurrency = CashCurrency.Try,
                    PostToLedger = false,
                    SourceBasketTransactionId = tx.Id,
                    Description = desc,
                },
                cancellationToken).ConfigureAwait(false);
        }
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

        Customer? customer = null;
        if (dto.CustomerId is { } custLookup)
            customer = await _unitOfWork.Customers.GetByIdAsync(custLookup, cancellationToken).ConfigureAwait(false);
        var validationError = ValidateSahisBasket(dto, customer);
        if (validationError != null)
            return BadRequest(validationError);

        var stampedDto = dto with { TransactionDate = TransactionDatePrecision.ApplySavePrecisionUtc(dto.TransactionDate) };
        var correlationId = Guid.NewGuid();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Kind = TransactionKind.StandardBasket,
            TransactionDate = stampedDto.TransactionDate,
            Description = dto.Description,
            CustomerId = dto.CustomerId,
            CorrelationId = correlationId,
            Quantity = 0,
            Milyem = 0,
            TotalLabour = 0,
            MilyemLabour = 0,
            IsSahisEmanet = dto.IsSahisEmanet,
            SahisEmanetMode = (SahisEmanetMode)dto.SahisEmanetMode,
            KasaHareketli = dto.KasaHareketli,
        };

        decimal totalBuyHas = 0;
        decimal totalSellHas = 0;
        decimal totalBuyTry = 0, totalSellTry = 0;
        decimal totalBuyUsd = 0, totalSellUsd = 0;
        decimal totalBuyEur = 0, totalSellEur = 0;
        decimal totalBuyGbp = 0, totalSellGbp = 0;

        foreach (var itemDto in dto.Items)
        {
            var r = ResolveBasketItem(itemDto);
            var lineCash = ResolveLineCash(r.HasGram, itemDto.Price, itemDto.LineTotal);
            var payCur = ParsePaymentCurrency(itemDto.PaymentCurrency);

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
                PaymentCurrency = payCur,
            };

            transaction.Items.Add(item);

            var itemCash = lineCash;
            if (itemDto.Direction == TransactionDirection.Purchase)
            {
                totalBuyHas += r.HasGram;
                switch (payCur)
                {
                    case CashCurrency.Try: totalBuyTry += itemCash; break;
                    case CashCurrency.Usd: totalBuyUsd += itemCash; break;
                    case CashCurrency.Eur: totalBuyEur += itemCash; break;
                    case CashCurrency.Gbp: totalBuyGbp += itemCash; break;
                }
            }
            else
            {
                totalSellHas += r.HasGram;
                switch (payCur)
                {
                    case CashCurrency.Try: totalSellTry += itemCash; break;
                    case CashCurrency.Usd: totalSellUsd += itemCash; break;
                    case CashCurrency.Eur: totalSellEur += itemCash; break;
                    case CashCurrency.Gbp: totalSellGbp += itemCash; break;
                }
            }

            await PostBasketItemPhysicalAsync(transaction, stampedDto, itemDto, r, lineCash, payCur, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }

        // Net values: positive = gold/cash inflow
        var netHasGram = totalBuyHas - totalSellHas;
        var netTry = totalSellTry - totalBuyTry;
        var netUsd = totalSellUsd - totalBuyUsd;
        var netEur = totalSellEur - totalBuyEur;
        var netGbp = totalSellGbp - totalBuyGbp;
        transaction.NetHasGram = Math.Round(netHasGram, 6);
        transaction.NetCashAmount = Math.Round(netTry, 6);
        transaction.NetCashAmountUsd = Math.Round(netUsd, 6);
        transaction.NetCashAmountEur = Math.Round(netEur, 6);
        transaction.NetCashAmountGbp = Math.Round(netGbp, 6);

        // Backward-compat header fields
        transaction.Direction = netHasGram >= 0 ? TransactionDirection.Purchase : TransactionDirection.Sale;
        transaction.HasGram = Math.Round(Math.Abs(netHasGram), 6);
        transaction.Price = Math.Round(Math.Abs(netTry), 6);

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
        foreach (var item in transaction.Items)
        {
            if (!SkipGoldLinkingForSale(dto, item.Direction))
                SyncGoldTransactionsForItem(item, transaction.Id, dto);
        }

        await AppendSahisEmanetLiabilityRowsAsync(transaction, stampedDto, customer, cancellationToken).ConfigureAwait(false);
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

            if (existingTransaction.Kind == TransactionKind.ForexExchange)
            {
                await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return BadRequest("Döviz işlemleri sepet düzenleme ile değiştirilemez; silip yeniden kaydedin.");
            }

            Customer? customer = null;
            if (dto.CustomerId is { } custLookup2)
                customer = await _unitOfWork.Customers.GetByIdAsync(custLookup2, cancellationToken).ConfigureAwait(false);
            var validationError = ValidateSahisBasket(dto, customer);
            if (validationError != null)
            {
                await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return BadRequest(validationError);
            }

            var stampedDto = dto with { TransactionDate = TransactionDatePrecision.ApplySavePrecisionUtc(dto.TransactionDate) };
            var dtoIdSet = idsInDto.ToHashSet();
            var correlationId = existingTransaction.CorrelationId ?? Guid.NewGuid();

            existingTransaction.TransactionDate = stampedDto.TransactionDate;
            existingTransaction.Description = dto.Description;
            existingTransaction.CustomerId = dto.CustomerId;
            existingTransaction.CorrelationId = correlationId;
            existingTransaction.IsSahisEmanet = dto.IsSahisEmanet;
            existingTransaction.SahisEmanetMode = (SahisEmanetMode)dto.SahisEmanetMode;
            existingTransaction.KasaHareketli = dto.KasaHareketli;

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
                    SyncGoldTransactionsForItem(entity, existingTransaction.Id, dto);
                }
                else
                {
                    var neu = CreateNewTransactionItemFromDto(itemDto, existingTransaction.Id);
                    existingTransaction.Items.Add(neu);
                    SyncGoldTransactionsForItem(neu, existingTransaction.Id, dto);
                }
            }

            await RebuildMovementsAndLedgerForTransactionAsync(existingTransaction, stampedDto, correlationId, customer, cancellationToken).ConfigureAwait(false);
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
        entity.PaymentCurrency = ParsePaymentCurrency(d.PaymentCurrency);
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
            PaymentCurrency = ParsePaymentCurrency(d.PaymentCurrency),
        };
    }

    /// <summary>
    /// Kısmi bağlama güncellemesi zaten engellendiği için RemainingGram güvenle has ile hizalanır.
    /// </summary>
    private void SyncGoldTransactionsForItem(TransactionItem item, Guid transactionId, BasketCreateDto dto)
    {
        if (SkipGoldLinkingForSale(dto, item.Direction))
        {
            var toClear = item.GoldTransactions.ToList();
            foreach (var gt in toClear)
            {
                _context.GoldTransactions.Remove(gt);
                item.GoldTransactions.Remove(gt);
            }
            return;
        }

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
        Customer? customer,
        CancellationToken cancellationToken)
    {
        var linkedBook = await _context.CustomerTransactions
            .Where(c => c.SourceBasketTransactionId == tx.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in linkedBook)
            _context.CustomerTransactions.Remove(row);

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
            var itemDto = new BasketItemDto(
                item.Id,
                item.Direction,
                item.Quantity,
                item.Milyem,
                item.PieceCount,
                item.UnitLabour,
                item.Price,
                null,
                item.Description,
                item.ProductTemplateId,
                (int)item.PaymentCurrency);
            var r = ResolveBasketItem(itemDto);
            var lineCash = item.Price ?? 0;
            await PostBasketItemPhysicalAsync(tx, dto, itemDto, r, lineCash, item.PaymentCurrency, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }

        await AppendSahisEmanetLiabilityRowsAsync(tx, dto, customer, cancellationToken).ConfigureAwait(false);
    }

    private static void RecalculateTransactionHeaderTotals(Transaction tx)
    {
        decimal totalBuyHas = 0, totalSellHas = 0;
        decimal totalBuyTry = 0, totalSellTry = 0;
        decimal totalBuyUsd = 0, totalSellUsd = 0;
        decimal totalBuyEur = 0, totalSellEur = 0;
        decimal totalBuyGbp = 0, totalSellGbp = 0;
        foreach (var item in tx.Items)
        {
            var cash = item.Price ?? 0;
            if (item.Direction == TransactionDirection.Purchase)
            {
                totalBuyHas += item.HasGram;
                switch (item.PaymentCurrency)
                {
                    case CashCurrency.Try: totalBuyTry += cash; break;
                    case CashCurrency.Usd: totalBuyUsd += cash; break;
                    case CashCurrency.Eur: totalBuyEur += cash; break;
                    case CashCurrency.Gbp: totalBuyGbp += cash; break;
                }
            }
            else
            {
                totalSellHas += item.HasGram;
                switch (item.PaymentCurrency)
                {
                    case CashCurrency.Try: totalSellTry += cash; break;
                    case CashCurrency.Usd: totalSellUsd += cash; break;
                    case CashCurrency.Eur: totalSellEur += cash; break;
                    case CashCurrency.Gbp: totalSellGbp += cash; break;
                }
            }
        }

        var netHasGram = totalBuyHas - totalSellHas;
        var netTry = totalSellTry - totalBuyTry;
        var netUsd = totalSellUsd - totalBuyUsd;
        var netEur = totalSellEur - totalBuyEur;
        var netGbp = totalSellGbp - totalBuyGbp;
        tx.NetHasGram = Math.Round(netHasGram, 6);
        tx.NetCashAmount = Math.Round(netTry, 6);
        tx.NetCashAmountUsd = Math.Round(netUsd, 6);
        tx.NetCashAmountEur = Math.Round(netEur, 6);
        tx.NetCashAmountGbp = Math.Round(netGbp, 6);
        tx.Direction = netHasGram >= 0 ? TransactionDirection.Purchase : TransactionDirection.Sale;
        tx.HasGram = Math.Round(Math.Abs(netHasGram), 6);
        tx.Price = Math.Round(Math.Abs(netTry), 6);
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

        await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (transaction.CorrelationId.HasValue
                && !transaction.Items.Any()
                && transaction.CashAmount.HasValue)
            {
                await _cashPegging.RestoreHybridPeggingFifoAsync(transaction.CorrelationId.Value, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _goldLinking.RemoveGoldTransactionsForTransactionAsync(id, cancellationToken).ConfigureAwait(false);

            var linkedBookRows = await _context.CustomerTransactions
                .Where(c => c.SourceBasketTransactionId == id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var row in linkedBookRows)
                _unitOfWork.CustomerTransactions.Delete(row);

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
            await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    // ── DTO mapping ──

    private static TransactionDto MapToDto(Transaction tx)
    {
        return new TransactionDto(
            Id: tx.Id,
            TransactionDate: tx.TransactionDate,
            Kind: tx.Kind,
            Direction: tx.Direction,
            IsSahisEmanet: tx.IsSahisEmanet,
            SahisEmanetMode: (int)tx.SahisEmanetMode,
            KasaHareketli: tx.KasaHareketli,
            NetHasGram: tx.NetHasGram,
            NetCashAmount: tx.NetCashAmount,
            NetCashAmountUsd: tx.NetCashAmountUsd,
            NetCashAmountEur: tx.NetCashAmountEur,
            NetCashAmountGbp: tx.NetCashAmountGbp,
            HasGram: tx.HasGram,
            Price: tx.Price,
            CashAmount: tx.CashAmount,
            EquivalentHasGram: tx.EquivalentHasGram,
            Description: tx.Description,
            CustomerId: tx.CustomerId,
            CustomerName: tx.Customer?.Name,
            CorrelationId: tx.CorrelationId,
            ForexBaseCurrency: tx.ForexBaseCurrency,
            ForexIsBuy: tx.ForexIsBuy,
            ForexAmountBase: tx.ForexAmountBase,
            ForexRateTryPerUnit: tx.ForexRateTryPerUnit,
            ForexCounterTry: tx.ForexCounterTry,
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
                ProductTemplateId: i.ProductTemplateId,
                PaymentCurrency: i.PaymentCurrency
            )).ToList()
        );
    }
}

// ── Request / Response DTOs ──

public record BasketCreateDto(
    DateTime TransactionDate,
    string? Description,
    Guid? CustomerId,
    List<BasketItemDto> Items,
    bool IsSahisEmanet = false,
    int SahisEmanetMode = 0,
    bool KasaHareketli = true
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
    Guid? ProductTemplateId,
    /// <summary>0=TL, 1=USD, 2=EUR, 3=GBP</summary>
    int? PaymentCurrency
);

public record TransactionDto(
    Guid Id,
    DateTime TransactionDate,
    TransactionKind Kind,
    TransactionDirection Direction,
    bool IsSahisEmanet,
    int SahisEmanetMode,
    bool KasaHareketli,
    decimal NetHasGram,
    decimal NetCashAmount,
    decimal NetCashAmountUsd,
    decimal NetCashAmountEur,
    decimal NetCashAmountGbp,
    decimal HasGram,
    decimal? Price,
    decimal? CashAmount,
    decimal? EquivalentHasGram,
    string? Description,
    Guid? CustomerId,
    string? CustomerName,
    Guid? CorrelationId,
    CashCurrency? ForexBaseCurrency,
    bool? ForexIsBuy,
    decimal? ForexAmountBase,
    decimal? ForexRateTryPerUnit,
    decimal? ForexCounterTry,
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
    Guid? ProductTemplateId,
    CashCurrency PaymentCurrency
);
