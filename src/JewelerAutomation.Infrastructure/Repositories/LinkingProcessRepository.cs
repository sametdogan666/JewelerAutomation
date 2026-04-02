using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class LinkingProcessRepository : Repository<LinkingProcess>, ILinkingProcessRepository
{
    public LinkingProcessRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<LinkingProcess?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<LinkingProcess>()
            .Include(p => p.SafeMovement)
            .Include(p => p.Details)
                .ThenInclude(d => d.GoldTransaction!)
                    .ThenInclude(g => g.Transaction)
            .Include(p => p.Details)
                .ThenInclude(d => d.GoldTransaction!)
                    .ThenInclude(g => g.TransactionItem)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<LinkingProcess>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<LinkingProcess>()
            .Include(p => p.SafeMovement)
            .OrderByDescending(p => p.LinkingDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
