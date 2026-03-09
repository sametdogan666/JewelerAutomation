using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

/// <summary>
/// Nakit Bağlama (Cash-to-Gold Pegging) işlemlerini yönetir.
/// </summary>
public interface ICashPeggingService
{
    /// <summary>
    /// Nakit bağlama işlemi yapar ve kaydeder.
    /// </summary>
    Task<CashPeggingLog> CreatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        string? notes = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tüm nakit bağlama geçmişini getirir.
    /// </summary>
    Task<IReadOnlyList<CashPeggingLog>> GetPeggingHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli tarih aralığındaki nakit bağlama kayıtlarını getirir.
    /// </summary>
    Task<IReadOnlyList<CashPeggingLog>> GetPeggingHistoryByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// En son nakit bağlama kaydını getirir.
    /// </summary>
    Task<CashPeggingLog?> GetLatestPeggingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Simülasyon: Verilen parametrelerle nakit bağlama yapılsaydı ne olurdu?
    /// Kayıt oluşturmaz, sadece hesaplama yapar.
    /// </summary>
    Task<PeggingSimulationResult> SimulatePeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Nakit bağlama simülasyon sonucu.
/// </summary>
public record PeggingSimulationResult(
    decimal CashBalance,
    decimal GoldBalance,
    decimal CashEquivalentHasGram,
    decimal TotalCapitalHasGram,
    decimal InitialCapitalHasGram,
    decimal TransactionProfitHasGram,
    decimal ExchangeRateProfitHasGram,
    decimal NetProfitHasGram
);
