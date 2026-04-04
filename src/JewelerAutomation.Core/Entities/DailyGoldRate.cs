using JewelerAutomation.Core.Enums;

namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Tarihsel has/döviz kuru — yalnızca saatlik ortalama veya gün kapanışı; canlı tick DB’ye yazılmaz.
/// </summary>
public class DailyGoldRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Saatlik için saat başı UTC; günlük kapanış için ilgili günün 00:00 UTC.</summary>
    public DateTime BucketStartUtc { get; set; }

    public GoldRateBucketKind Kind { get; set; }

    public decimal AvgHasTryBuy { get; set; }
    public decimal AvgHasTrySell { get; set; }
    public decimal AvgHasTryMid { get; set; }

    public decimal? ClosingUsdTryMid { get; set; }

    public int SampleCount { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
