using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class CashPeggingLogRepository : ICashPeggingLogRepository
{
    private readonly AppDbContext _context;

    public CashPeggingLogRepository(AppDbContext context) => _context = context;

    public async Task<CashPeggingLog> AddAsync(CashPeggingLog entity, CancellationToken cancellationToken = default)
    {
        await _context.CashPeggingLogs.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<CashPeggingLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.CashPeggingLogs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CashPeggingLog>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.CashPeggingLogs
            .OrderByDescending(x => x.PeggingDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CashPeggingLog>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => await _context.CashPeggingLogs
            .Where(x => x.PeggingDate >= from && x.PeggingDate <= to)
            .OrderByDescending(x => x.PeggingDate)
            .ToListAsync(cancellationToken);

    public async Task<CashPeggingLog?> GetLatestAsync(CancellationToken cancellationToken = default)
        => await _context.CashPeggingLogs
            .OrderByDescending(x => x.PeggingDate)
            .FirstOrDefaultAsync(cancellationToken);

    public void Update(CashPeggingLog entity)
        => _context.CashPeggingLogs.Update(entity);

    public void Delete(CashPeggingLog entity)
        => _context.CashPeggingLogs.Remove(entity);
}
