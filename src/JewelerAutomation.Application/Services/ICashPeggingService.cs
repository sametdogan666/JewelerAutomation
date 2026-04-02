using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public interface ICashPeggingService
{
    Task<CashPeggingLog> CreatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        string? notes = null,
        Guid? userId = null,
        decimal? pegCashFromSafe = null,
        decimal? pegHasGram = null,
        CancellationToken cancellationToken = default);

    Task DeletePeggingAsync(Guid peggingId, CancellationToken cancellationToken = default);

    /// <summary>İşlemlerden nakit bağlama silinmeden önce: FIFO satış pozisyonunu geri yükler.</summary>
    Task RestoreHybridPeggingFifoAsync(Guid correlationId, CancellationToken cancellationToken = default);

    Task<CashPeggingLog> UpdatePeggingAsync(
        Guid peggingId,
        decimal newGoldPricePerGram,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashPeggingLog>> GetPeggingHistoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashPeggingLog>> GetPeggingHistoryByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<CashPeggingLog?> GetLatestPeggingAsync(CancellationToken cancellationToken = default);

    Task<PeggingSimulationResult> SimulatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        decimal? pegCashFromSafe = null,
        decimal? pegHasGram = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Period-based profit model (excludes physical gold inventory):
///   transactionProfit  = totalSalesHasGram - totalPurchasesHasGram
///   cashPeggingProfit  = periodCashBalance / goldPrice
///   netProfitHasGram   = cashPeggingProfit - transactionProfit
///   netProfitTL        = netProfitHasGram * goldPrice
/// </summary>
public record PeggingSimulationResult(
    decimal PeriodCashBalance,
    decimal GoldBalanceInSafe,
    decimal CashEquivalentHasGram,
    decimal TotalSalesHasGram,
    decimal TotalPurchasesHasGram,
    decimal TransactionProfitHasGram,
    decimal NetProfitHasGram,
    decimal NetProfitTL,
    decimal SafeCashBalance,
    decimal LedgerPeriodCashBalance
);
