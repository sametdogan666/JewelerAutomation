using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Core.Enums;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class DailyGoldRateRepository : IDailyGoldRateRepository
{
    private readonly AppDbContext _context;

    public DailyGoldRateRepository(AppDbContext context) => _context = context;

    public async Task<DailyGoldRate?> GetByBucketAsync(
        DateTime bucketStartUtc,
        GoldRateBucketKind kind,
        CancellationToken cancellationToken = default)
        => await _context.DailyGoldRates
            .FirstOrDefaultAsync(
                x => x.BucketStartUtc == bucketStartUtc && x.Kind == kind,
                cancellationToken);

    public async Task AddAsync(DailyGoldRate entity, CancellationToken cancellationToken = default)
    {
        await _context.DailyGoldRates.AddAsync(entity, cancellationToken);
    }

    public void Update(DailyGoldRate entity) => _context.DailyGoldRates.Update(entity);

    public async Task<(decimal Mid, DateTime RecordedAtUtc)?> GetLatestRecordedMidAsync(CancellationToken cancellationToken = default)
    {
        var row = await _context.DailyGoldRates
            .AsNoTracking()
            .Where(x => x.AvgHasTryMid > 0)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.BucketStartUtc)
            .Select(x => new { x.AvgHasTryMid, x.RecordedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        return row == null ? null : (row.AvgHasTryMid, row.RecordedAtUtc);
    }

    public Task<bool> HasAnyRowsAsync(CancellationToken cancellationToken = default)
        => _context.DailyGoldRates.AsNoTracking().AnyAsync(cancellationToken);
}
