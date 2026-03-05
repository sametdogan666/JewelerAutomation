using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class CustomerMovementRepository : ICustomerMovementRepository
{
    private readonly AppDbContext _context;

    public CustomerMovementRepository(AppDbContext context) => _context = context;

    public async Task<Core.Entities.CustomerMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.CustomerMovements
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Core.Entities.CustomerMovement>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _context.CustomerMovements
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.TransactionDate)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Cari/Şahıs bakiye: SUM(HasGram) - Excel TOPLAM HESAP = SUM(G4:G500)
    /// </summary>
    public async Task<decimal> GetBalanceByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _context.CustomerMovements
            .Where(x => x.CustomerId == customerId)
            .SumAsync(x => x.HasGram, cancellationToken);

    public async Task<Core.Entities.CustomerMovement> AddAsync(Core.Entities.CustomerMovement entity, CancellationToken cancellationToken = default)
    {
        await _context.CustomerMovements.AddAsync(entity, cancellationToken);
        return entity;
    }
}
