using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JewelerAutomation.Infrastructure.Repositories;

public class CashToGoldConversionRepository : Repository<CashToGoldConversion>, ICashToGoldConversionRepository
{
    public CashToGoldConversionRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<CashToGoldConversion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Context.CashToGoldConversions
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public override async Task<IReadOnlyList<CashToGoldConversion>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Context.CashToGoldConversions
            .Include(c => c.Customer)
            .OrderByDescending(c => c.TransactionDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CashToGoldConversion>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => await Context.CashToGoldConversions
            .Where(c => c.TransactionDate >= startDate && c.TransactionDate <= endDate)
            .Include(c => c.Customer)
            .OrderByDescending(c => c.TransactionDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CashToGoldConversion>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await Context.CashToGoldConversions
            .Where(c => c.CustomerId == customerId)
            .Include(c => c.Customer)
            .OrderByDescending(c => c.TransactionDate)
            .ToListAsync(cancellationToken);

    public async Task<decimal> GetTotalConvertedGoldAsync(CancellationToken cancellationToken = default)
        => await Context.CashToGoldConversions
            .SumAsync(c => c.ConvertedGoldHas, cancellationToken);

    public async Task<bool> AnyForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await Context.CashToGoldConversions.AnyAsync(c => c.CustomerId == customerId, cancellationToken);
}
