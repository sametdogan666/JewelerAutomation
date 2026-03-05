using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _context;

    public InventoryRepository(AppDbContext context) => _context = context;

    public async Task<Core.Entities.Inventory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Inventories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Core.Entities.Inventory>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Inventories.OrderBy(x => x.Code).ToListAsync(cancellationToken);

    public async Task<Core.Entities.Inventory> AddAsync(Core.Entities.Inventory entity, CancellationToken cancellationToken = default)
    {
        await _context.Inventories.AddAsync(entity, cancellationToken);
        return entity;
    }

    public void Update(Core.Entities.Inventory entity) => _context.Inventories.Update(entity);
}
