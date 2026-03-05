using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class SafeMovementRepository : ISafeMovementRepository
{
    private readonly AppDbContext _context;

    public SafeMovementRepository(AppDbContext context) => _context = context;

    public async Task<Core.Entities.SafeMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.SafeMovements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Core.Entities.SafeMovement>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.SafeMovements.OrderBy(x => x.TransactionDate).ToListAsync(cancellationToken);

    /// <summary>
    /// Kasa bakiyesi: SUM(HasGram) - Excel'deki ANA SERMAYE (HAS-GR) = SUM(D3:D600)
    /// </summary>
    public async Task<decimal> GetTotalHasGramBalanceAsync(CancellationToken cancellationToken = default)
        => await _context.SafeMovements.SumAsync(x => x.HasGram, cancellationToken);

    public async Task<Core.Entities.SafeMovement> AddAsync(Core.Entities.SafeMovement entity, CancellationToken cancellationToken = default)
    {
        await _context.SafeMovements.AddAsync(entity, cancellationToken);
        return entity;
    }
}
