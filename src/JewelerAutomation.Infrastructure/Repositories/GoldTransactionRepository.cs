using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class GoldTransactionRepository : Repository<GoldTransaction>, IGoldTransactionRepository
{
    public GoldTransactionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<decimal> GetTotalOpenHasGramAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<GoldTransaction>()
            .Where(g => !g.IsFullyLinked && g.RemainingGram > 0)
            .SumAsync(g => g.RemainingGram, cancellationToken);
    }

    public async Task<decimal> GetTotalOpenHasGramInPeriodAsync(
        DateTime? periodStart,
        DateTime? periodEnd,
        CancellationToken cancellationToken = default)
    {
        IQueryable<GoldTransaction> q = Context.Set<GoldTransaction>()
            .Include(g => g.Transaction)
            .Where(g => !g.IsFullyLinked && g.RemainingGram > 0);

        if (periodStart.HasValue)
            q = q.Where(g => g.Transaction.TransactionDate >= periodStart.Value);
        if (periodEnd.HasValue)
            q = q.Where(g => g.Transaction.TransactionDate <= periodEnd.Value);

        return await q.SumAsync(g => g.RemainingGram, cancellationToken);
    }

    public async Task<IReadOnlyList<GoldTransaction>> GetFifoOpenOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<GoldTransaction>()
            .Include(g => g.Transaction)
            .Include(g => g.TransactionItem)
            .Where(g => !g.IsFullyLinked && g.RemainingGram > 0)
            .OrderBy(g => g.Transaction.TransactionDate)
            .ThenBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoldTransaction>> GetFifoOpenOrderedInPeriodAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        return await Context.Set<GoldTransaction>()
            .Include(g => g.Transaction)
            .Include(g => g.TransactionItem)
            .Where(g => !g.IsFullyLinked && g.RemainingGram > 0
                        && g.Transaction.TransactionDate >= periodStart
                        && g.Transaction.TransactionDate <= periodEnd)
            .OrderBy(g => g.Transaction.TransactionDate)
            .ThenBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoldTransaction>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<GoldTransaction>()
            .Where(g => g.TransactionId == transactionId)
            .ToListAsync(cancellationToken);
    }

    public async Task DetachTransactionItemLinksForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var rows = await Context.Set<GoldTransaction>()
            .IgnoreQueryFilters()
            .Where(g => g.TransactionId == transactionId && g.TransactionItemId != null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var g in rows)
        {
            g.TransactionItemId = null;
            g.UpdatedAt = now;
        }
    }

    public async Task<bool> HasPartialLinkForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<GoldTransaction>()
            .AnyAsync(g => g.TransactionId == transactionId && g.RemainingGram < g.OriginalHasGram, cancellationToken);
    }
}
