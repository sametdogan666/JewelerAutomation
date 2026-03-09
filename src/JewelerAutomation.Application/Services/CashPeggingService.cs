using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public class CashPeggingService : ICashPeggingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;

    public CashPeggingService(IUnitOfWork unitOfWork, IAccountingService accounting)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
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

        var log = new CashPeggingLog
        {
            PeggingDate = DateTime.UtcNow,
            CashAmount = simulation.CashBalance,
            GoldPricePerGram = goldPricePerGram,
            EquivalentHasGram = simulation.CashEquivalentHasGram,
            PhysicalGoldAtTime = simulation.GoldBalance,
            TotalCapitalHasGram = simulation.TotalCapitalHasGram,
            PeriodStartDate = periodStart,
            PeriodEndDate = periodEnd,
            TransactionProfitHasGram = simulation.TransactionProfitHasGram,
            ExchangeRateProfitHasGram = simulation.ExchangeRateProfitHasGram,
            NetProfitHasGram = simulation.NetProfitHasGram,
            Notes = notes,
            UserId = userId
        };

        await _unitOfWork.CashPeggingLogs.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return log;
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
        // 1. Dönem başı ve sonu için sermaye hesapla
        var initialCapital = await _accounting.GetInitialCapitalAsync(cancellationToken);
        var cashBalanceResult = await _accounting.GetCashBalanceAsync(cancellationToken);
        var cashBalance = cashBalanceResult.NetCashBalance;
        var goldInSafe = await _unitOfWork.SafeMovements.GetTotalHasGramBalanceAsync(cancellationToken);

        // 2. Nakit karşılığı has gram
        var cashEquivalentHasGram = goldPricePerGram > 0 ? cashBalance / goldPricePerGram : 0;

        // 3. Toplam sermaye (Has cinsinden)
        var totalCapitalHasGram = goldInSafe + cashEquivalentHasGram;

        // 4. Net kâr = Toplam Sermaye - Başlangıç Sermayesi
        var netProfitHasGram = totalCapitalHasGram - initialCapital;

        // 5. İşlem kârı: Dönem içindeki alış-satış marjı
        var transactionProfit = await CalculateTransactionProfitAsync(periodStart, periodEnd, cancellationToken);

        // 6. Kur farkı kârı: Net Kâr - İşlem Kârı
        var exchangeRateProfit = netProfitHasGram - transactionProfit;

        return new PeggingSimulationResult(
            CashBalance: cashBalance,
            GoldBalance: goldInSafe,
            CashEquivalentHasGram: cashEquivalentHasGram,
            TotalCapitalHasGram: totalCapitalHasGram,
            InitialCapitalHasGram: initialCapital,
            TransactionProfitHasGram: transactionProfit,
            ExchangeRateProfitHasGram: exchangeRateProfit,
            NetProfitHasGram: netProfitHasGram
        );
    }

    /// <summary>
    /// Dönem içindeki işlem kârını hesaplar (alış-satış marjı).
    /// Basitleştirilmiş: Satış Has - Alış Has
    /// </summary>
    private async Task<decimal> CalculateTransactionProfitAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var transactions = await _unitOfWork.Transactions.GetByDateRangeAsync(periodStart, periodEnd, cancellationToken);

        decimal totalSalesHas = 0;
        decimal totalPurchasesHas = 0;

        foreach (var tx in transactions)
        {
            if (tx.Direction == TransactionDirection.Sale)
                totalSalesHas += tx.HasGram;
            else if (tx.Direction == TransactionDirection.Purchase)
                totalPurchasesHas += tx.HasGram;
        }

        return totalSalesHas - totalPurchasesHas;
    }
}
