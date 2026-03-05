using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface IInventoryRepository
{
    Task<Inventory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inventory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Inventory> AddAsync(Inventory entity, CancellationToken cancellationToken = default);
    void Update(Inventory entity);
}
