using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Repositories;

public class GoldRatesRepository : IGoldRatesRepository
{
    private readonly AppDbContext _context;

    public GoldRatesRepository(AppDbContext context) => _context = context;

    public async Task<GoldRateRow?> GetByEffectiveDateAsync(
        DateOnly effectiveDate,
        bool isManual,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.GoldRates
            .AsNoTracking()
            .Where(x => x.EffectiveDate == effectiveDate && x.IsManual == isManual)
            .Select(x => new GoldRateRow(x.HasTryPerGramMid, x.UsdTryMid, x.RecordedAtUtc, x.IsManual))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row;
    }

    public Task<bool> HasManualForDateAsync(DateOnly effectiveDate, CancellationToken cancellationToken = default)
        => _context.GoldRates.AsNoTracking().AnyAsync(
            x => x.EffectiveDate == effectiveDate && x.IsManual && x.HasTryPerGramMid > 0,
            cancellationToken);

    public async Task UpsertManualAsync(
        DateOnly effectiveDate,
        decimal hasTryPerGramMid,
        decimal? usdTryMid,
        Guid? setByUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.GoldRates
            .FirstOrDefaultAsync(x => x.EffectiveDate == effectiveDate && x.IsManual, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.HasTryPerGramMid = hasTryPerGramMid;
            existing.UsdTryMid = usdTryMid;
            existing.RecordedAtUtc = now;
            existing.SetByUserId = setByUserId;
        }
        else
        {
            await _context.GoldRates.AddAsync(new GoldRate
            {
                Id = Guid.NewGuid(),
                EffectiveDate = effectiveDate,
                HasTryPerGramMid = hasTryPerGramMid,
                UsdTryMid = usdTryMid,
                IsManual = true,
                RecordedAtUtc = now,
                SetByUserId = setByUserId,
            }, cancellationToken).ConfigureAwait(false);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAutoAsync(
        DateOnly effectiveDate,
        decimal hasTryPerGramMid,
        decimal? usdTryMid,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.GoldRates
            .FirstOrDefaultAsync(x => x.EffectiveDate == effectiveDate && !x.IsManual, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.HasTryPerGramMid = hasTryPerGramMid;
            existing.UsdTryMid = usdTryMid;
            existing.RecordedAtUtc = now;
        }
        else
        {
            await _context.GoldRates.AddAsync(new GoldRate
            {
                Id = Guid.NewGuid(),
                EffectiveDate = effectiveDate,
                HasTryPerGramMid = hasTryPerGramMid,
                UsdTryMid = usdTryMid,
                IsManual = false,
                RecordedAtUtc = now,
                SetByUserId = null,
            }, cancellationToken).ConfigureAwait(false);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
