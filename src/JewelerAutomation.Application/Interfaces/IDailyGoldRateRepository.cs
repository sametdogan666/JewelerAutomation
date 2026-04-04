using JewelerAutomation.Core.Entities;
using JewelerAutomation.Core.Enums;

namespace JewelerAutomation.Application.Interfaces;

public interface IDailyGoldRateRepository
{
    Task<DailyGoldRate?> GetByBucketAsync(DateTime bucketStartUtc, GoldRateBucketKind kind, CancellationToken cancellationToken = default);

    Task AddAsync(DailyGoldRate entity, CancellationToken cancellationToken = default);

    void Update(DailyGoldRate entity);

    /// <summary>Son kayıtlı orta kur (saatlik/günlük); canlı önbellek boşsa fallback.</summary>
    Task<(decimal Mid, DateTime RecordedAtUtc)?> GetLatestRecordedMidAsync(CancellationToken cancellationToken = default);

    Task<bool> HasAnyRowsAsync(CancellationToken cancellationToken = default);
}
