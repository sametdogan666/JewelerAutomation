using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Utilities;
using JewelerAutomation.Core.Entities;
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
    /// Sadece manuel eklenen kasa hareketlerini döndür (alış-satıştan otomatik oluşanlar hariç).
    /// </summary>
    public async Task<IReadOnlyList<Core.Entities.SafeMovement>> GetManualMovementsAsync(CancellationToken cancellationToken = default)
        => await _context.SafeMovements
            .Where(x => x.SourceTransactionId == null)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Kasa hareketleri — imzalı fiziki toplam ile aynı mantık (geriye dönük uyumluluk).
    /// </summary>
    public async Task<decimal> GetTotalHasGramBalanceAsync(CancellationToken cancellationToken = default)
        => await GetPhysicalVaultNetHasGramAsync(cancellationToken).ConfigureAwait(false);

    public async Task<decimal> GetPhysicalVaultNetHasGramAsync(CancellationToken cancellationToken = default)
    {
        var movements = await _context.SafeMovements
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return movements.Sum(SafeMovementPhysicalVault.GetSignedHasGramContribution);
    }

    public async Task<Core.Entities.SafeMovement> AddAsync(Core.Entities.SafeMovement entity, CancellationToken cancellationToken = default)
    {
        await _context.SafeMovements.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<Core.Entities.SafeMovement>> FindByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
        => await _context.SafeMovements
            .Where(m => m.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);

    public async Task<bool> AnyVaultMovementForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _context.SafeMovements
            .AnyAsync(m => m.SourceTransactionId != null
                && _context.Transactions.Any(t => t.Id == m.SourceTransactionId && t.CustomerId == customerId),
                cancellationToken);

    public void Update(Core.Entities.SafeMovement entity) => _context.SafeMovements.Update(entity);

    public void Delete(Core.Entities.SafeMovement entity) => _context.SafeMovements.Remove(entity);
}
