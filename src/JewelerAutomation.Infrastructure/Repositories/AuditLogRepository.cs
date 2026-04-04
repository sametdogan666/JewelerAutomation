using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int skip, int take, CancellationToken cancellationToken = default)
        => await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.AuditLogs.CountAsync(cancellationToken);
}
