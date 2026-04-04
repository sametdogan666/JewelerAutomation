using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context) => _context = context;

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Items)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => await _context.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Items)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

    public async Task<Transaction> AddAsync(Transaction entity, CancellationToken cancellationToken = default)
    {
        await _context.Transactions.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<Transaction>> FindByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
        => await _context.Transactions
            .Include(t => t.Items)
            .Where(t => t.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);

    public async Task<bool> AnyByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _context.Transactions.AnyAsync(t => t.CustomerId == customerId, cancellationToken);

    public void Update(Transaction entity) => _context.Transactions.Update(entity);

    public void Delete(Transaction entity) => _context.Transactions.Remove(entity);

    public void RemoveItems(IEnumerable<TransactionItem> items)
        => _context.TransactionItems.RemoveRange(items);
}
