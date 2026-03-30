using Microsoft.Extensions.Logging;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class CashPeggingService : ICashPeggingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;
    private readonly ILedgerService _ledger;
    private readonly ILogger<CashPeggingService> _logger;

    public CashPeggingService(
        IUnitOfWork unitOfWork,
        IAccountingService accounting,
        ILedgerService ledger,
        ILogger<CashPeggingService> logger)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task<CashPeggingLog> CreatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        string? notes = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var simulation = await SimulatePeggingAsync(periodStart, periodEnd, goldPricePerGram, cancellationToken);

        _logger.LogInformation(
            "Pegging: Sales={Sales}, Purchases={Purchases}, TxProfit={TxProfit}, " +
            "CashEquiv={CashEquiv}, NetProfit={NetProfit} Has Gr, NetProfitTL={NetProfitTL}",
            simulation.TotalSalesHasGram, simulation.TotalPurchasesHasGram,
            simulation.TransactionProfitHasGram, simulation.CashEquivalentHasGram,
            simulation.NetProfitHasGram, simulation.NetProfitTL);

        var cashAmount = simulation.PeriodCashBalance;
        var equivalentHasGram = simulation.CashEquivalentHasGram;
        var peggingDate = DateTime.UtcNow;
        var correlationId = Guid.NewGuid();
        var description = notes ?? $"Nakit Bağlama: {cashAmount:N2} TL → {equivalentHasGram:N6} Has Gr";

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
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
                NetProfitHasGram = simulation.NetProfitHasGram,
                Notes = notes,
                UserId = userId
            };

            await _unitOfWork.CashPeggingLogs.AddAsync(log, cancellationToken);

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
                    CorrelationId = correlationId
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
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Cash pegging committed: LogId={LogId}, CorrelationId={CorrId}, Cash={Cash} TL → Gold={Gold} Has Gr",
                log.Id, correlationId, cashAmount, equivalentHasGram);

            return log;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Cash pegging failed, transaction rolled back");
            throw;
        }
    }

    public async Task DeletePeggingAsync(Guid peggingId, CancellationToken cancellationToken = default)
    {
        var log = await _unitOfWork.CashPeggingLogs.GetByIdAsync(peggingId, cancellationToken)
            ?? throw new InvalidOperationException($"CashPeggingLog {peggingId} not found.");

        _unitOfWork.CashPeggingLogs.Delete(log);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        var description = notes ?? $"Nakit Bağlama: {oldCash:N2} TL → {newEquivalentHasGram:N6} Has Gr";

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            log.GoldPricePerGram = newGoldPricePerGram;
            log.EquivalentHasGram = newEquivalentHasGram;
            log.TotalCapitalHasGram = log.PhysicalGoldAtTime + newEquivalentHasGram;
            if (notes != null) log.Notes = notes;
            _unitOfWork.CashPeggingLogs.Update(log);

            var linkedTransactions = await _unitOfWork.Transactions.FindByCorrelationIdAsync(
                log.CorrelationId, cancellationToken);

            foreach (var tx in linkedTransactions)
            {
                tx.Quantity = Math.Abs(newEquivalentHasGram);
                tx.HasGram = Math.Abs(newEquivalentHasGram);
                tx.Price = Math.Abs(oldCash);
                tx.Description = description;
                _unitOfWork.Transactions.Update(tx);
            }

            var linkedMovements = await _unitOfWork.SafeMovements.FindByCorrelationIdAsync(
                log.CorrelationId, cancellationToken);

            foreach (var mv in linkedMovements)
            {
                mv.Gram = Math.Abs(newEquivalentHasGram);
                mv.HasGram = Math.Abs(newEquivalentHasGram);
                mv.Description = description;
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Pegging updated: LogId={LogId}, CorrelationId={CorrId}, NewPrice={Price}, NewHas={Has}",
                log.Id, log.CorrelationId, newGoldPricePerGram, newEquivalentHasGram);

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
        CancellationToken cancellationToken = default)
    {
        if (goldPricePerGram <= 0)
            throw new ArgumentException("Has fiyatı sıfırdan büyük olmalıdır.", nameof(goldPricePerGram));

        // Period-specific transaction data
        var periodSummary = await _accounting.GetPeriodTransactionSummaryAsync(
            periodStart, periodEnd, cancellationToken);

        // Total gold in safe (for display only, NOT used in profit)
        var totalBalances = await _ledger.GetBalancesAsync(cancellationToken);

        // Period cash balance from ledger
        var periodBalances = await _ledger.GetBalancesByPeriodAsync(periodStart, periodEnd, cancellationToken);
        var periodCash = periodBalances.TotalCashBalance;

        // Profit calculation (excludes physical gold inventory)
        var totalSalesHas = periodSummary.TotalSalesHasGram;
        var totalPurchasesHas = periodSummary.TotalPurchasesHasGram;
        var transactionProfitHas = totalSalesHas - totalPurchasesHas;

        var cashEquivalentHas = periodCash / goldPricePerGram;

        var netProfitHasGram = cashEquivalentHas - transactionProfitHas;
        var netProfitTL = netProfitHasGram * goldPricePerGram;

        _logger.LogInformation(
            "Simulate: Period={Start:yyyy-MM-dd} to {End:yyyy-MM-dd}, GoldPrice={Price}, " +
            "SalesHas={Sales}, PurchasesHas={Purchases}, TxProfit={TxProfit}, " +
            "PeriodCash={Cash}, CashEquivHas={CashEquiv}, " +
            "NetProfitHas={NetProfit}, NetProfitTL={NetTL}, GoldInSafe={Safe}",
            periodStart, periodEnd, goldPricePerGram,
            totalSalesHas, totalPurchasesHas, transactionProfitHas,
            periodCash, cashEquivalentHas,
            netProfitHasGram, netProfitTL, totalBalances.TotalGoldBalance);

        return new PeggingSimulationResult(
            PeriodCashBalance: Math.Round(periodCash, 2),
            GoldBalanceInSafe: Math.Round(totalBalances.TotalGoldBalance, 6),
            CashEquivalentHasGram: Math.Round(cashEquivalentHas, 6),
            TotalSalesHasGram: Math.Round(totalSalesHas, 6),
            TotalPurchasesHasGram: Math.Round(totalPurchasesHas, 6),
            TransactionProfitHasGram: Math.Round(transactionProfitHas, 6),
            NetProfitHasGram: Math.Round(netProfitHasGram, 6),
            NetProfitTL: Math.Round(netProfitTL, 2)
        );
    }
}
