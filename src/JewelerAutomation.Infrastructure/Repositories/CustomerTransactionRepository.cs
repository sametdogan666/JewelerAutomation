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

    public async Task<CustomerTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CustomerTransactions
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CustomerTransactions
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public void Update(CustomerTransaction entity)
    {
        _context.CustomerTransactions.Update(entity);
    }

    public void Delete(CustomerTransaction entity)
    {
        _context.CustomerTransactions.Remove(entity);
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

    /// <inheritdoc />
    public async Task<CustomerBookBalances> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var transactions = await _context.CustomerTransactions
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        decimal gold = 0;
        decimal tryB = 0, usd = 0, eur = 0, gbp = 0;

        void AddCash(CashCurrency cur, decimal amt)
        {
            switch (cur)
            {
                case CashCurrency.Try: tryB += amt; break;
                case CashCurrency.Usd: usd += amt; break;
                case CashCurrency.Eur: eur += amt; break;
                case CashCurrency.Gbp: gbp += amt; break;
            }
        }

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
                    AddCash(t.CashCurrency, -t.CashAmount);
                    break;
                case CustomerTransactionType.CashCollection:
                    AddCash(t.CashCurrency, t.CashAmount);
                    break;
                case CustomerTransactionType.OpeningBalance:
                    ApplyOpeningBalance(t, ref gold, ref tryB, ref usd, ref eur, ref gbp, AddCash);
                    break;
                case CustomerTransactionType.SahisEmanetLiability:
                    gold += t.GoldHas;
                    break;
            }
        }

        return new CustomerBookBalances(gold, tryB, usd, eur, gbp);
    }

    private static void ApplyOpeningBalance(
        CustomerTransaction t,
        ref decimal gold,
        ref decimal tryB,
        ref decimal usd,
        ref decimal eur,
        ref decimal gbp,
        Action<CashCurrency, decimal> addCash)
    {
        if (t.OpeningAssetKind is not { } kind || t.OpeningCustomerIsCreditor is not { } isCreditor)
            return;

        var sign = isCreditor ? 1m : -1m;
        if (kind == SahisOpeningAssetKind.Gold)
        {
            gold += sign * t.GoldHas;
            return;
        }

        var cur = kind switch
        {
            SahisOpeningAssetKind.Try => CashCurrency.Try,
            SahisOpeningAssetKind.Usd => CashCurrency.Usd,
            SahisOpeningAssetKind.Eur => CashCurrency.Eur,
            SahisOpeningAssetKind.Gbp => CashCurrency.Gbp,
            _ => CashCurrency.Try
        };
        addCash(cur, sign * t.CashAmount);
    }

    public async Task<bool> AnyForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _context.CustomerTransactions.AnyAsync(x => x.CustomerId == customerId, cancellationToken);
}
