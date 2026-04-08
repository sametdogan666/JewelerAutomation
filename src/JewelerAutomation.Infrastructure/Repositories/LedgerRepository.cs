using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JewelerAutomation.Infrastructure.Repositories;

public class LedgerRepository : Repository<LedgerEntry>, ILedgerRepository
{
    public LedgerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<decimal> GetGoldBalanceAsync(CancellationToken cancellationToken = default)
    {
        var goldIn = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.GoldIn)
            .SumAsync(e => e.GoldHasAmount, cancellationToken);

        var goldOut = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.GoldOut)
            .SumAsync(e => e.GoldHasAmount, cancellationToken);

        return goldIn - goldOut;
    }

    public Task<decimal> GetCashBalanceAsync(CancellationToken cancellationToken = default)
        => GetCashBalanceForCurrencyAsync(CashCurrency.Try, cancellationToken);

    public async Task<decimal> GetCashBalanceForCurrencyAsync(CashCurrency currency, CancellationToken cancellationToken = default)
    {
        var cashIn = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.CashIn && e.CashCurrency == currency)
            .SumAsync(e => e.CashAmount, cancellationToken);

        var cashOut = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.CashOut && e.CashCurrency == currency)
            .SumAsync(e => e.CashAmount, cancellationToken);

        return cashIn - cashOut;
    }

    public async Task<decimal> GetGoldBalanceByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var goldIn = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.GoldIn && e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .SumAsync(e => e.GoldHasAmount, cancellationToken);

        var goldOut = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.GoldOut && e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .SumAsync(e => e.GoldHasAmount, cancellationToken);

        return goldIn - goldOut;
    }

    public async Task<decimal> GetCashBalanceByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var cashIn = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.CashIn
                        && e.CashCurrency == CashCurrency.Try
                        && e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .SumAsync(e => e.CashAmount, cancellationToken);

        var cashOut = await Context.LedgerEntries
            .Where(e => e.EntryType == LedgerEntryType.CashOut
                        && e.CashCurrency == CashCurrency.Try
                        && e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .SumAsync(e => e.CashAmount, cancellationToken);

        return cashIn - cashOut;
    }

    public async Task<decimal> GetGoldBalanceByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var goldIn = await Context.LedgerEntries
            .Where(e => e.CustomerId == customerId && e.EntryType == LedgerEntryType.GoldIn)
            .SumAsync(e => e.GoldHasAmount, cancellationToken);

        var goldOut = await Context.LedgerEntries
            .Where(e => e.CustomerId == customerId && e.EntryType == LedgerEntryType.GoldOut)
            .SumAsync(e => e.GoldHasAmount, cancellationToken);

        return goldIn - goldOut;
    }

    public async Task<decimal> GetCashBalanceByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var cashIn = await Context.LedgerEntries
            .Where(e => e.CustomerId == customerId && e.EntryType == LedgerEntryType.CashIn && e.CashCurrency == CashCurrency.Try)
            .SumAsync(e => e.CashAmount, cancellationToken);

        var cashOut = await Context.LedgerEntries
            .Where(e => e.CustomerId == customerId && e.EntryType == LedgerEntryType.CashOut && e.CashCurrency == CashCurrency.Try)
            .SumAsync(e => e.CashAmount, cancellationToken);

        return cashIn - cashOut;
    }

    public async Task<IEnumerable<LedgerEntry>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await Context.LedgerEntries
            .Where(e => e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .OrderBy(e => e.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LedgerEntry>> GetByCustomerAndPeriodAsync(Guid customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await Context.LedgerEntries
            .Where(e => e.CustomerId == customerId && e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .OrderBy(e => e.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await Context.LedgerEntries.CountAsync(cancellationToken);
    }

    public async Task<bool> AnyEntryForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await Context.LedgerEntries.AnyAsync(e => e.CustomerId == customerId, cancellationToken);
}
