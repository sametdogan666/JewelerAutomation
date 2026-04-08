using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ISafeMovementRepository
{
    Task<SafeMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SafeMovement>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SafeMovement>> GetManualMovementsAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalHasGramBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tüm kasa hareketlerinden (manuel + sepet) imzalı has toplamı — fiziki brüt kasa.
    /// </summary>
    Task<decimal> GetPhysicalVaultNetHasGramAsync(CancellationToken cancellationToken = default);
    Task<SafeMovement> AddAsync(SafeMovement entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SafeMovement>> FindByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<bool> AnyVaultMovementForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    void Update(SafeMovement entity);
    void Delete(SafeMovement entity);
}
