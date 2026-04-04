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
/// Dönem işlem açığı T = satış has − alış has. Kasada kalan nakit R, bu bağlamada kullanılan C.
/// S = C/P + R/P (tüm kasa nakdinin seçilen fiyattaki has karşılığı). Kısmi bağlamada:
///   toplam net = S − T; mühürlenen = E×(S−T)/S; bekleyen = R_h×(S−T)/S (S&gt;0).
/// </summary>
public record PeggingSimulationResult(
    decimal PeriodCashBalance,
    decimal GoldBalanceInSafe,
    decimal CashEquivalentHasGram,
    decimal TotalSalesHasGram,
    decimal TotalPurchasesHasGram,
    decimal TransactionProfitHasGram,
    /// <summary>Kasadaki tüm nakit + bu bağlama hası ile T&apos;ye göre net (S − T).</summary>
    decimal NetProfitHasGram,
    decimal NetProfitTL,
    decimal SafeCashBalance,
    decimal LedgerPeriodCashBalance,
    decimal RemainingSafeCashTl,
    decimal RemainingCashAsHasGram,
    decimal TotalCashCoverAsHasGram,
    decimal UnbackedGoldDebtHasGram,
    decimal RealizedNetProfitHasGram,
    decimal RealizedNetProfitTl,
    decimal PendingEstimatedNetHasGram,
    decimal PendingEstimatedNetTl
);
