using System.Linq;
using Microsoft.Extensions.Logging;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class CashPeggingService : ICashPeggingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;
    private readonly ILedgerService _ledger;
    private readonly IGoldLinkingService _goldLinking;
    private readonly ILogger<CashPeggingService> _logger;

    public CashPeggingService(
        IUnitOfWork unitOfWork,
        IAccountingService accounting,
        ILedgerService ledger,
        IGoldLinkingService goldLinking,
        ILogger<CashPeggingService> logger)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
        _ledger = ledger;
        _goldLinking = goldLinking;
        _logger = logger;
    }

    public async Task<CashPeggingLog> CreatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        string? notes = null,
        Guid? userId = null,
        decimal? pegCashFromSafe = null,
        decimal? pegHasGram = null,
        CancellationToken cancellationToken = default)
    {
        var simulation = await SimulatePeggingAsync(
            periodStart, periodEnd, goldPricePerGram, pegCashFromSafe, pegHasGram, cancellationToken);

        _logger.LogInformation(
            "Pegging: Sales={Sales}, Purchases={Purchases}, TxProfit={TxProfit}, " +
            "CashEquiv={CashEquiv}, RealizedNet={Realized} Has Gr, TotalNet={TotalNet} Has Gr",
            simulation.TotalSalesHasGram, simulation.TotalPurchasesHasGram,
            simulation.TransactionProfitHasGram, simulation.CashEquivalentHasGram,
            simulation.RealizedNetProfitHasGram, simulation.NetProfitHasGram);

        var cashAmount = simulation.PeriodCashBalance;
        var equivalentHasGram = simulation.CashEquivalentHasGram;
        var peggingDate = DateTime.UtcNow;
        var correlationId = Guid.NewGuid();
        var description = BuildPeggingTransactionDescription(cashAmount, equivalentHasGram, goldPricePerGram, notes);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var netProfitHasGram = simulation.RealizedNetProfitHasGram;

            var log = new CashPeggingLog
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                PeggingDate = peggingDate,
                CashAmount = cashAmount,
                GoldPricePerGram = goldPricePerGram,
                EquivalentHasGram = equivalentHasGram,
                PhysicalGoldAtTime = simulation.GoldBalanceInSafe,
                TotalCapitalHasGram = simulation.GoldBalanceInSafe + equivalentHasGram,
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEnd,
                TransactionProfitHasGram = simulation.TransactionProfitHasGram,
                ExchangeRateProfitHasGram = 0,
                NetProfitHasGram = netProfitHasGram,
                Notes = notes,
                UserId = userId
            };

            await _unitOfWork.CashPeggingLogs.AddAsync(log, cancellationToken);

            // ── Main pegging transaction (cash → gold) ──
            if (cashAmount != 0 && equivalentHasGram != 0)
            {
                var transaction = new Transaction
                {
                    TransactionDate = peggingDate,
                    Direction = TransactionDirection.Purchase,
                    Quantity = Math.Abs(equivalentHasGram),
                    Milyem = 1000m,
                    PieceCount = null,
                    UnitLabour = null,
                    TotalLabour = 0,
                    HasGram = Math.Abs(equivalentHasGram),
                    Price = Math.Abs(cashAmount),
                    Description = description,
                    MilyemLabour = 0,
                    CustomerId = null,
                    CorrelationId = correlationId,
                    NetHasGram = Math.Abs(equivalentHasGram),
                    NetCashAmount = -Math.Abs(cashAmount),
                    CashAmount = Math.Abs(cashAmount),
                    EquivalentHasGram = Math.Abs(equivalentHasGram)
                };

                await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);

                var safeMovement = new SafeMovement
                {
                    TransactionDate = peggingDate,
                    Gram = Math.Abs(equivalentHasGram),
                    Milyem = 1000m,
                    HasGram = Math.Abs(equivalentHasGram),
                    Description = description,
                    MovementType = SafeMovementType.Income,
                    SourceTransactionId = transaction.Id,
                    CorrelationId = correlationId
                };

                await _unitOfWork.SafeMovements.AddAsync(safeMovement, cancellationToken);

                await _ledger.RecordTransactionAsync(
                    transactionDate: peggingDate,
                    direction: TransactionDirection.Purchase,
                    goldHasAmount: Math.Abs(equivalentHasGram),
                    cashAmount: Math.Abs(cashAmount),
                    referenceId: transaction.Id,
                    customerId: null,
                    description: description,
                    correlationId: correlationId,
                    cancellationToken: cancellationToken
                );

                var fifoConsumed = await _goldLinking.ConsumeFifoForHybridPeggingAsync(
                    periodStart, periodEnd, Math.Abs(equivalentHasGram), cancellationToken).ConfigureAwait(false);
                foreach (var (goldTxId, amt) in fifoConsumed)
                {
                    log.FifoDetails.Add(new CashPeggingFifoDetail
                    {
                        GoldTransactionId = goldTxId,
                        AmountDeducted = amt
                    });
                }
            }

            // ── Profit realization (reporting-only — NO ledger entry) ──
            // The profit is already inherent in the pegging purchase: equivalentHasGram > salesHasGram.
            // Creating a separate GoldIn ledger entry would double-count this profit.
            if (Math.Abs(netProfitHasGram) > 0.000001m)
            {
                var profitDesc = $"Kâr Gerçekleştirme ({periodStart:dd.MM.yyyy}–{periodEnd:dd.MM.yyyy}): " +
                                 $"{(netProfitHasGram >= 0 ? "+" : "")}{netProfitHasGram:N6} Has Gr";

                var profitMovement = new SafeMovement
                {
                    TransactionDate = peggingDate,
                    Gram = Math.Abs(netProfitHasGram),
                    Milyem = 1000m,
                    HasGram = Math.Abs(netProfitHasGram),
                    Description = profitDesc,
                    MovementType = SafeMovementType.ProfitRealization,
                    SourceTransactionId = null,
                    CorrelationId = correlationId
                };

                await _unitOfWork.SafeMovements.AddAsync(profitMovement, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Cash pegging committed: LogId={LogId}, CorrelationId={CorrId}, " +
                "Cash={Cash} TL → Gold={Gold} Has Gr, RealizedProfit={Profit} Has Gr (totalNet={TotalNet})",
                log.Id, correlationId, cashAmount, equivalentHasGram, netProfitHasGram, simulation.NetProfitHasGram);

            return log;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Cash pegging failed, transaction rolled back");
            throw;
        }
    }

    public async Task RestoreHybridPeggingFifoAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var log = await _unitOfWork.CashPeggingLogs
            .GetByCorrelationIdWithFifoDetailsAsync(correlationId, cancellationToken)
            .ConfigureAwait(false);
        if (log?.FifoDetails == null || log.FifoDetails.Count == 0)
            return;

        await _goldLinking.RestoreHybridPeggingConsumptionsAsync(
            log.FifoDetails.Select(d => (d.GoldTransactionId, d.AmountDeducted)),
            cancellationToken).ConfigureAwait(false);

        _unitOfWork.CashPeggingLogs.RemoveFifoDetails(log.FifoDetails.ToList());
    }

    public async Task DeletePeggingAsync(Guid peggingId, CancellationToken cancellationToken = default)
    {
        var log = await _unitOfWork.CashPeggingLogs.GetByIdAsync(peggingId, cancellationToken)
            ?? throw new InvalidOperationException($"CashPeggingLog {peggingId} not found.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await RestoreHybridPeggingFifoAsync(log.CorrelationId, cancellationToken).ConfigureAwait(false);

            _unitOfWork.CashPeggingLogs.Delete(log);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Pegging delete failed, rolled back");
            throw;
        }

        _logger.LogInformation(
            "Pegging deleted (soft): LogId={LogId}, CorrelationId={CorrId} — cascaded to linked records",
            log.Id, log.CorrelationId);
    }

    public async Task<CashPeggingLog> UpdatePeggingAsync(
        Guid peggingId,
        decimal newGoldPricePerGram,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (newGoldPricePerGram <= 0)
            throw new ArgumentException("Has fiyatı sıfırdan büyük olmalıdır.", nameof(newGoldPricePerGram));

        var log = await _unitOfWork.CashPeggingLogs.GetByIdAsync(peggingId, cancellationToken)
            ?? throw new InvalidOperationException($"CashPeggingLog {peggingId} not found.");

        var oldCash = log.CashAmount;
        var newEquivalentHasGram = oldCash != 0 ? Math.Round(oldCash / newGoldPricePerGram, 6) : 0;
        var description = BuildPeggingTransactionDescription(oldCash, newEquivalentHasGram, newGoldPricePerGram, notes);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var newNetProfitHas = newEquivalentHasGram - log.TransactionProfitHasGram;

            log.GoldPricePerGram = newGoldPricePerGram;
            log.EquivalentHasGram = newEquivalentHasGram;
            log.TotalCapitalHasGram = log.PhysicalGoldAtTime + newEquivalentHasGram;
            log.NetProfitHasGram = newNetProfitHas;
            if (notes != null) log.Notes = notes;
            _unitOfWork.CashPeggingLogs.Update(log);

            var linkedTransactions = await _unitOfWork.Transactions.FindByCorrelationIdAsync(
                log.CorrelationId, cancellationToken);

            foreach (var tx in linkedTransactions)
            {
                tx.Quantity = Math.Abs(newEquivalentHasGram);
                tx.HasGram = Math.Abs(newEquivalentHasGram);
                tx.Price = Math.Abs(oldCash);
                tx.NetHasGram = Math.Abs(newEquivalentHasGram);
                tx.NetCashAmount = -Math.Abs(oldCash);
                tx.CashAmount = Math.Abs(oldCash);
                tx.EquivalentHasGram = Math.Abs(newEquivalentHasGram);
                tx.Description = description;
                _unitOfWork.Transactions.Update(tx);
            }

            var linkedMovements = await _unitOfWork.SafeMovements.FindByCorrelationIdAsync(
                log.CorrelationId, cancellationToken);

            SafeMovement? profitMovement = null;
            foreach (var mv in linkedMovements)
            {
                if (mv.MovementType == SafeMovementType.ProfitRealization)
                {
                    mv.Gram = Math.Abs(newNetProfitHas);
                    mv.HasGram = Math.Abs(newNetProfitHas);
                    mv.Description = $"Kâr Gerçekleştirme ({log.PeriodStartDate:dd.MM.yyyy}–{log.PeriodEndDate:dd.MM.yyyy}): " +
                                     $"{(newNetProfitHas >= 0 ? "+" : "")}{newNetProfitHas:N6} Has Gr";
                    profitMovement = mv;
                }
                else
                {
                    mv.Gram = Math.Abs(newEquivalentHasGram);
                    mv.HasGram = Math.Abs(newEquivalentHasGram);
                    mv.Description = description;
                }
                _unitOfWork.SafeMovements.Update(mv);
            }

            // Delete old ledger entries and re-create with new amounts
            await _ledger.DeleteEntriesByCorrelationAsync(log.CorrelationId, cancellationToken);

            if (oldCash != 0 && newEquivalentHasGram != 0)
            {
                var refId = linkedTransactions.FirstOrDefault()?.Id ?? log.Id;
                await _ledger.RecordTransactionAsync(
                    transactionDate: log.PeggingDate,
                    direction: TransactionDirection.Purchase,
                    goldHasAmount: Math.Abs(newEquivalentHasGram),
                    cashAmount: Math.Abs(oldCash),
                    referenceId: refId,
                    customerId: null,
                    description: description,
                    correlationId: log.CorrelationId,
                    cancellationToken: cancellationToken
                );
            }

            // ProfitRealization is reporting-only — no ledger entry needed.

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Pegging updated: LogId={LogId}, CorrelationId={CorrId}, NewPrice={Price}, " +
                "NewHas={Has}, NewProfit={Profit}",
                log.Id, log.CorrelationId, newGoldPricePerGram, newEquivalentHasGram, newNetProfitHas);

            return log;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Pegging update failed, transaction rolled back");
            throw;
        }
    }

    public async Task<IReadOnlyList<CashPeggingLog>> GetPeggingHistoryAsync(CancellationToken cancellationToken = default)
        => await _unitOfWork.CashPeggingLogs.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<CashPeggingLog>> GetPeggingHistoryByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
        => await _unitOfWork.CashPeggingLogs.GetByDateRangeAsync(from, to, cancellationToken);

    public async Task<CashPeggingLog?> GetLatestPeggingAsync(CancellationToken cancellationToken = default)
        => await _unitOfWork.CashPeggingLogs.GetLatestAsync(cancellationToken);

    public async Task<PeggingSimulationResult> SimulatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        decimal? pegCashFromSafe = null,
        decimal? pegHasGram = null,
        CancellationToken cancellationToken = default)
    {
        if (goldPricePerGram <= 0)
            throw new ArgumentException("Has fiyatı sıfırdan büyük olmalıdır.", nameof(goldPricePerGram));

        if (pegCashFromSafe is > 0 && pegHasGram is > 0)
            throw new ArgumentException("Aynı anda hem nakit hem has hedefi verilemez.");

        // Period-specific transaction data
        var periodSummary = await _accounting.GetPeriodTransactionSummaryAsync(
            periodStart, periodEnd, cancellationToken);

        // Total gold in safe (for display only, NOT used in profit)
        var totalBalances = await _ledger.GetBalancesAsync(cancellationToken);

        // Period cash balance from ledger
        var periodBalances = await _ledger.GetBalancesByPeriodAsync(periodStart, periodEnd, cancellationToken);
        var ledgerPeriodCash = Math.Round(periodBalances.TotalCashBalance, 2);
        var safeCash = Math.Round(totalBalances.TotalCashBalance, 2);

        // Profit calculation (excludes physical gold inventory)
        var totalSalesHas = periodSummary.TotalSalesHasGram;
        var totalPurchasesHas = periodSummary.TotalPurchasesHasGram;
        var transactionProfitHas = totalSalesHas - totalPurchasesHas;

        decimal cashAmount;
        if (pegHasGram is > 0)
        {
            var needed = Math.Round(pegHasGram.Value * goldPricePerGram, 2);
            cashAmount = Math.Min(needed, safeCash);
        }
        else if (pegCashFromSafe is > 0)
        {
            cashAmount = Math.Min(Math.Round(pegCashFromSafe.Value, 2), safeCash);
        }
        else
        {
            cashAmount = ledgerPeriodCash;
        }

        var cashEquivalentHas = goldPricePerGram > 0 ? cashAmount / goldPricePerGram : 0;
        var remainingSafeTl = Math.Max(0, safeCash - cashAmount);
        var remainingAsHas = goldPricePerGram > 0 ? remainingSafeTl / goldPricePerGram : 0m;
        var totalCoverHas = cashEquivalentHas + remainingAsHas;

        var T = transactionProfitHas;
        decimal netProfitHasGram;
        decimal netProfitTL;
        decimal realizedNetHasGram;
        decimal realizedNetTl;
        decimal pendingNetHasGram;
        decimal pendingNetTl;
        decimal unbackedDebtHas;

        if (totalCoverHas <= 0.0000001m)
        {
            netProfitHasGram = cashEquivalentHas - T;
            netProfitTL = Math.Round(netProfitHasGram * goldPricePerGram, 2);
            realizedNetHasGram = netProfitHasGram;
            realizedNetTl = netProfitTL;
            pendingNetHasGram = 0;
            pendingNetTl = 0;
            unbackedDebtHas = Math.Max(0, T - cashEquivalentHas);
        }
        else
        {
            netProfitHasGram = totalCoverHas - T;
            netProfitTL = Math.Round(netProfitHasGram * goldPricePerGram, 2);
            realizedNetHasGram = Math.Round(cashEquivalentHas * (totalCoverHas - T) / totalCoverHas, 6);
            pendingNetHasGram = Math.Round(remainingAsHas * (totalCoverHas - T) / totalCoverHas, 6);
            realizedNetTl = Math.Round(realizedNetHasGram * goldPricePerGram, 2);
            pendingNetTl = Math.Round(pendingNetHasGram * goldPricePerGram, 2);
            unbackedDebtHas = Math.Max(0, T - totalCoverHas);
        }

        _logger.LogInformation(
            "Simulate: Period={Start:yyyy-MM-dd} to {End:yyyy-MM-dd}, GoldPrice={Price}, " +
            "SalesHas={Sales}, PurchasesHas={Purchases}, TxProfit={TxProfit}, " +
            "PegCash={Cash}, CashEquivHas={CashEquiv}, CoverHas={Cover}, " +
            "NetHas={Net}, Realized={Real}, Pending={Pending}, Unbacked={Unbacked}, GoldInSafe={Safe}",
            periodStart, periodEnd, goldPricePerGram,
            totalSalesHas, totalPurchasesHas, T,
            cashAmount, cashEquivalentHas, totalCoverHas,
            netProfitHasGram, realizedNetHasGram, pendingNetHasGram, unbackedDebtHas,
            totalBalances.TotalGoldBalance);

        return new PeggingSimulationResult(
            PeriodCashBalance: cashAmount,
            GoldBalanceInSafe: Math.Round(totalBalances.TotalGoldBalance, 6),
            CashEquivalentHasGram: Math.Round(cashEquivalentHas, 6),
            TotalSalesHasGram: Math.Round(totalSalesHas, 6),
            TotalPurchasesHasGram: Math.Round(totalPurchasesHas, 6),
            TransactionProfitHasGram: Math.Round(transactionProfitHas, 6),
            NetProfitHasGram: Math.Round(netProfitHasGram, 6),
            NetProfitTL: netProfitTL,
            SafeCashBalance: safeCash,
            LedgerPeriodCashBalance: ledgerPeriodCash,
            RemainingSafeCashTl: Math.Round(remainingSafeTl, 2),
            RemainingCashAsHasGram: Math.Round(remainingAsHas, 6),
            TotalCashCoverAsHasGram: Math.Round(totalCoverHas, 6),
            UnbackedGoldDebtHasGram: Math.Round(unbackedDebtHas, 6),
            RealizedNetProfitHasGram: Math.Round(realizedNetHasGram, 6),
            RealizedNetProfitTl: realizedNetTl,
            PendingEstimatedNetHasGram: Math.Round(pendingNetHasGram, 6),
            PendingEstimatedNetTl: pendingNetTl
        );
    }

    /// <summary>
    /// İşlemler listesinde net kolonları ve açıklama için tutarlı metin (fiyat TL/gr dahil).
    /// </summary>
    private static string BuildPeggingTransactionDescription(
        decimal cashAmount,
        decimal equivalentHasGram,
        decimal goldPricePerGram,
        string? notes)
    {
        var core =
            $"Nakit Bağlama: {cashAmount:N2} TL → {equivalentHasGram:N4} Has Gr @ {goldPricePerGram:N2} TL/gr";
        return string.IsNullOrWhiteSpace(notes) ? core : $"{core} · {notes.Trim()}";
    }
}
