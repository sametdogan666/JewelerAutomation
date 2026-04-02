using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class GoldLinkingService : IGoldLinkingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledger;
    private readonly IGoldTransactionRepository _goldTransactions;
    private readonly ILinkingProcessRepository _linkingProcesses;
    private readonly IRepository<LinkingDetail> _linkingDetails;

    public GoldLinkingService(
        IUnitOfWork unitOfWork,
        ILedgerService ledger,
        IGoldTransactionRepository goldTransactions,
        ILinkingProcessRepository linkingProcesses,
        IRepository<LinkingDetail> linkingDetails)
    {
        _unitOfWork = unitOfWork;
        _ledger = ledger;
        _goldTransactions = goldTransactions;
        _linkingProcesses = linkingProcesses;
        _linkingDetails = linkingDetails;
    }

    public Task<decimal> GetOpenHasPositionAsync(
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (periodStart.HasValue && periodEnd.HasValue)
            return _goldTransactions.GetTotalOpenHasGramInPeriodAsync(periodStart, periodEnd, cancellationToken);
        return _goldTransactions.GetTotalOpenHasGramAsync(cancellationToken);
    }

    public async Task RegisterSaleGoldPositionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction.Items.Any())
        {
            foreach (var item in transaction.Items.Where(i => i.Direction == TransactionDirection.Sale))
            {
                await _goldTransactions.AddAsync(new GoldTransaction
                {
                    TransactionId = transaction.Id,
                    TransactionItemId = item.Id,
                    OriginalHasGram = item.HasGram,
                    RemainingGram = item.HasGram,
                    IsFullyLinked = false
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (transaction.Direction == TransactionDirection.Sale && transaction.HasGram > 0)
        {
            await _goldTransactions.AddAsync(new GoldTransaction
            {
                TransactionId = transaction.Id,
                TransactionItemId = null,
                OriginalHasGram = transaction.HasGram,
                RemainingGram = transaction.HasGram,
                IsFullyLinked = false
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoveGoldTransactionsForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var list = await _goldTransactions.GetByTransactionIdAsync(transactionId, cancellationToken).ConfigureAwait(false);
        foreach (var g in list)
            _goldTransactions.Remove(g);
    }

    public Task<bool> HasPartialLinkForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
        => _goldTransactions.HasPartialLinkForTransactionAsync(transactionId, cancellationToken);

    public async Task<FifoLinkingSimulationResult> SimulateFifoLinkingAsync(
        decimal targetAmountGram,
        decimal targetPricePerGram,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (targetAmountGram <= 0)
            throw new ArgumentException("Hedef has gram pozitif olmalıdır.", nameof(targetAmountGram));
        if (targetPricePerGram <= 0)
            throw new ArgumentException("Has fiyatı pozitif olmalıdır.", nameof(targetPricePerGram));

        var open = await GetOpenHasPositionAsync(periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
        var queue = await GetFifoQueueAsync(periodStart, periodEnd, cancellationToken).ConfigureAwait(false);

        var remaining = targetAmountGram;
        decimal totalProfitTl = 0;

        foreach (var gt in queue)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, gt.RemainingGram);
            if (take <= 0) continue;

            var salePricePerGram = GetEffectiveSalePricePerGram(gt);
            totalProfitTl += take * (salePricePerGram - targetPricePerGram);
            remaining -= take;
        }

        var sufficient = remaining <= 0.0001m;

        return new FifoLinkingSimulationResult(
            TargetAmountGram: Math.Round(targetAmountGram, 4),
            TargetPricePerGram: Math.Round(targetPricePerGram, 4),
            EstimatedProfitTl: Math.Round(totalProfitTl, 4),
            OpenHasPositionGram: Math.Round(open, 4),
            SufficientOpenPosition: sufficient
        );
    }

    public async Task<LinkingProcessResultDto> ProcessPartialLinkingAsync(
        decimal targetAmountGram,
        decimal targetPricePerGram,
        string? notes,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (targetAmountGram <= 0)
            throw new ArgumentException("Hedef has gram pozitif olmalıdır.", nameof(targetAmountGram));
        if (targetPricePerGram <= 0)
            throw new ArgumentException("Has fiyatı pozitif olmalıdır.", nameof(targetPricePerGram));

        var processId = Guid.NewGuid();
        var linkingDate = DateTime.UtcNow;
        var cashCost = Math.Round(targetAmountGram * targetPricePerGram, 4);

        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var queue = await GetFifoQueueAsync(periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
            var remaining = targetAmountGram;
            decimal totalProfitTl = 0;
            var detailRows = new List<(Guid GoldTxId, decimal Deducted)>();

            foreach (var gt in queue)
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, gt.RemainingGram);
                if (take <= 0) continue;

                var salePricePerGram = GetEffectiveSalePricePerGram(gt);
                totalProfitTl += take * (salePricePerGram - targetPricePerGram);

                gt.RemainingGram = Math.Round(gt.RemainingGram - take, 4);
                if (gt.RemainingGram <= 0.0001m)
                {
                    gt.RemainingGram = 0;
                    gt.IsFullyLinked = true;
                }
                else
                {
                    gt.IsFullyLinked = false;
                }

                _goldTransactions.Update(gt);
                detailRows.Add((gt.Id, take));
                remaining -= take;
            }

            if (remaining > 0.0001m)
                throw new InvalidOperationException(
                    $"Yetersiz açık satış (FIFO) pozisyonu. Eksik: {remaining:N4} Has Gr.");

            await _ledger.RecordLinkingFifoPurchaseAsync(
                linkingDate,
                cashCost,
                targetAmountGram,
                processId,
                $"FIFO Nakit Bağlama: {targetAmountGram:N4} Has @ {targetPricePerGram:N4} TL/Gr",
                cancellationToken).ConfigureAwait(false);

            SafeMovement? profitMovement = null;
            var profitGoldGram = totalProfitTl > 0.0001m
                ? Math.Round(totalProfitTl / targetPricePerGram, 4)
                : 0m;

            if (profitGoldGram > 0.0001m)
            {
                profitMovement = new SafeMovement
                {
                    TransactionDate = linkingDate,
                    Gram = profitGoldGram,
                    Milyem = 1000m,
                    HasGram = profitGoldGram,
                    Description =
                        $"Kâr Gerçekleştirme (FIFO Nakit Bağlama): {totalProfitTl:N2} TL ≈ {profitGoldGram:N4} Has Gr",
                    MovementType = SafeMovementType.ProfitRealization,
                    SourceTransactionId = null,
                    CorrelationId = processId
                };
                await _unitOfWork.SafeMovements.AddAsync(profitMovement, cancellationToken).ConfigureAwait(false);
            }

            var process = new LinkingProcess
            {
                Id = processId,
                LinkingDate = linkingDate,
                TargetAmount = Math.Round(targetAmountGram, 4),
                TargetPrice = Math.Round(targetPricePerGram, 4),
                TotalProfit = Math.Round(totalProfitTl, 4),
                SafeMovementId = profitMovement?.Id,
                Notes = notes
            };

            foreach (var (goldTxId, deducted) in detailRows)
            {
                process.Details.Add(new LinkingDetail
                {
                    LinkingProcessId = processId,
                    GoldTransactionId = goldTxId,
                    AmountDeducted = Math.Round(deducted, 4)
                });
            }

            await _linkingProcesses.AddAsync(process, cancellationToken).ConfigureAwait(false);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            return new LinkingProcessResultDto(
                process.Id,
                process.LinkingDate,
                process.TargetAmount,
                process.TargetPrice,
                process.TotalProfit,
                process.SafeMovementId,
                process.Notes
            );
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task CancelLinkingAsync(Guid linkingProcessId, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var process = await _linkingProcesses.GetByIdWithDetailsAsync(linkingProcessId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Bağlantı işlemi bulunamadı: {linkingProcessId}");

            await _ledger.DeleteEntriesByReferenceAsync(LedgerReferenceType.LinkingProcess, process.Id, cancellationToken).ConfigureAwait(false);

            if (process.SafeMovementId.HasValue)
            {
                await _ledger.DeleteEntriesByReferenceAsync(
                    LedgerReferenceType.SafeMovement,
                    process.SafeMovementId.Value,
                    cancellationToken).ConfigureAwait(false);

                var sm = await _unitOfWork.SafeMovements.GetByIdAsync(process.SafeMovementId.Value, cancellationToken).ConfigureAwait(false);
                if (sm != null)
                    _unitOfWork.SafeMovements.Delete(sm);
            }

            var detailSnapshot = process.Details.ToList();
            foreach (var d in detailSnapshot)
            {
                var gt = await _goldTransactions.GetByIdAsync(d.GoldTransactionId, cancellationToken).ConfigureAwait(false);
                if (gt == null) continue;

                gt.RemainingGram = Math.Round(gt.RemainingGram + d.AmountDeducted, 4);
                if (gt.RemainingGram > gt.OriginalHasGram)
                    gt.RemainingGram = gt.OriginalHasGram;

                gt.IsFullyLinked = gt.RemainingGram <= 0.0001m;
                _goldTransactions.Update(gt);

                _linkingDetails.Remove(d);
            }

            _linkingProcesses.Remove(process);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<LinkingProcessListItemDto>> GetLinkingHistoryAsync(CancellationToken cancellationToken = default)
    {
        var fifo = await _linkingProcesses.GetAllOrderedAsync(cancellationToken).ConfigureAwait(false);
        var fifoDtos = fifo.Select(p => new LinkingProcessListItemDto(
            p.Id,
            p.LinkingDate,
            p.TargetAmount,
            p.TargetPrice,
            p.TotalProfit,
            p.SafeMovementId,
            p.Notes)).ToList();

        var hybridLogs = await _unitOfWork.CashPeggingLogs.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var hybridDtos = new List<LinkingProcessListItemDto>();
        foreach (var log in hybridLogs)
        {
            var txs = await _unitOfWork.Transactions.FindByCorrelationIdAsync(log.CorrelationId, cancellationToken)
                .ConfigureAwait(false);
            var header = txs.FirstOrDefault(t => !t.Items.Any() && t.CashAmount.HasValue);
            if (header == null)
                continue;

            var profitTl = Math.Round(log.NetProfitHasGram * log.GoldPricePerGram, 4);
            hybridDtos.Add(new LinkingProcessListItemDto(
                Id: header.Id,
                LinkingDate: log.PeggingDate,
                TargetAmount: log.EquivalentHasGram,
                TargetPrice: log.GoldPricePerGram,
                TotalProfit: profitTl,
                SafeMovementId: null,
                Notes: log.Notes,
                Kind: "Hybrid",
                PeriodStartDate: log.PeriodStartDate,
                PeriodEndDate: log.PeriodEndDate,
                CashAmount: log.CashAmount,
                NetProfitHasGram: log.NetProfitHasGram));
        }

        return fifoDtos.Concat(hybridDtos)
            .OrderByDescending(x => x.LinkingDate)
            .ToList();
    }

    public async Task<IReadOnlyList<(Guid GoldTransactionId, decimal AmountDeducted)>> ConsumeFifoForHybridPeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal targetGram,
        CancellationToken cancellationToken = default)
    {
        if (targetGram <= 0.0000001m)
            return Array.Empty<(Guid, decimal)>();

        var queue = await GetFifoQueueAsync(periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
        var remaining = targetGram;
        var details = new List<(Guid GoldTransactionId, decimal AmountDeducted)>();

        foreach (var gt in queue)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, gt.RemainingGram);
            if (take <= 0) continue;

            gt.RemainingGram = Math.Round(gt.RemainingGram - take, 4);
            if (gt.RemainingGram <= 0.0001m)
            {
                gt.RemainingGram = 0;
                gt.IsFullyLinked = true;
            }
            else
                gt.IsFullyLinked = false;

            _goldTransactions.Update(gt);
            details.Add((gt.Id, take));
            remaining -= take;
        }

        return details;
    }

    public async Task RestoreHybridPeggingConsumptionsAsync(
        IEnumerable<(Guid GoldTransactionId, decimal AmountDeducted)> details,
        CancellationToken cancellationToken = default)
    {
        foreach (var (goldTxId, amt) in details)
        {
            var gt = await _goldTransactions.GetByIdAsync(goldTxId, cancellationToken).ConfigureAwait(false);
            if (gt == null) continue;

            gt.RemainingGram = Math.Round(gt.RemainingGram + amt, 4);
            if (gt.RemainingGram > gt.OriginalHasGram)
                gt.RemainingGram = gt.OriginalHasGram;

            gt.IsFullyLinked = gt.RemainingGram <= 0.0001m;
            _goldTransactions.Update(gt);
        }
    }

    private async Task<IReadOnlyList<GoldTransaction>> GetFifoQueueAsync(
        DateTime? periodStart,
        DateTime? periodEnd,
        CancellationToken cancellationToken)
    {
        if (periodStart.HasValue && periodEnd.HasValue)
        {
            return await _goldTransactions
                .GetFifoOpenOrderedInPeriodAsync(periodStart.Value, periodEnd.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return await _goldTransactions.GetFifoOpenOrderedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static decimal GetEffectiveSalePricePerGram(GoldTransaction gt)
    {
        if (gt.TransactionItemId.HasValue && gt.TransactionItem != null)
        {
            var item = gt.TransactionItem;
            if (item.HasGram <= 0) return 0;
            return (item.Price ?? 0) / item.HasGram;
        }

        var tx = gt.Transaction;
        if (tx.HasGram <= 0) return 0;
        return (tx.Price ?? 0) / tx.HasGram;
    }
}
