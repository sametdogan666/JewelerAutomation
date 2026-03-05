using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface ISafeMovementRepository
{
    Task<SafeMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SafeMovement>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SafeMovement>> GetManualMovementsAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalHasGramBalanceAsync(CancellationToken cancellationToken = default);
    Task<SafeMovement> AddAsync(SafeMovement entity, CancellationToken cancellationToken = default);
    void Update(SafeMovement entity);
    void Delete(SafeMovement entity);
}
