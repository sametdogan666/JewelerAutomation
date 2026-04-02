using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface IGoldTransactionRepository : IRepository<GoldTransaction>
{
    Task<decimal> GetTotalOpenHasGramAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dönem içi satışlardan kalan açık has (FIFO kapsamı).
    /// </summary>
    Task<decimal> GetTotalOpenHasGramInPeriodAsync(
        DateTime? periodStart,
        DateTime? periodEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FIFO: IsFullyLinked == false, kalan gram &gt; 0, en eski satış önce.
    /// </summary>
    Task<IReadOnlyList<GoldTransaction>> GetFifoOpenOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// FIFO satış tarihi dönem içinde olan açık pozisyonlar, en eski önce.
    /// </summary>
    Task<IReadOnlyList<GoldTransaction>> GetFifoOpenOrderedInPeriodAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoldTransaction>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// İşlemde kısmi bağlantı var mı (güncelleme/silme için).
    /// </summary>
    Task<bool> HasPartialLinkForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
