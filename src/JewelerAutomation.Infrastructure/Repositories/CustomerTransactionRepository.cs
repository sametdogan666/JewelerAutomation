using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class CustomerTransactionRepository : ICustomerTransactionRepository
{
    private readonly AppDbContext _context;

    public CustomerTransactionRepository(AppDbContext context) => _context = context;

    public async Task<CustomerTransaction> AddAsync(CustomerTransaction entity, CancellationToken cancellationToken = default)
    {
        await _context.CustomerTransactions.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<CustomerTransaction>> GetStatementAsync(Guid customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        IQueryable<CustomerTransaction> query = _context.CustomerTransactions.Where(x => x.CustomerId == customerId);
        if (fromDate.HasValue)
            query = query.Where(x => x.TransactionDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(x => x.TransactionDate <= toDate.Value);
        return await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gold: GoldPurchase adds, GoldSale subtracts. Cash: CashCollection adds (credit to customer), CashPayment subtracts.
    /// GoldBalance positive = gold we hold for customer / owe to customer. CashBalance positive = we owe customer (they have credit).
    /// </summary>
    public async Task<(decimal GoldBalance, decimal CashBalance)> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var transactions = await _context.CustomerTransactions
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        decimal gold = 0;
        decimal cash = 0;
        foreach (var t in transactions)
        {
            switch (t.TransactionType)
            {
                case CustomerTransactionType.GoldPurchase:
                    gold += t.GoldHas;
                    break;
                case CustomerTransactionType.GoldSale:
                    gold -= t.GoldHas;
                    break;
                case CustomerTransactionType.CashPayment:
                    cash -= t.CashAmount; // customer paid us
                    break;
                case CustomerTransactionType.CashCollection:
                    cash += t.CashAmount; // we paid customer
                    break;
            }
        }
        return (gold, cash);
    }
}
