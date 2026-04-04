namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Günlük HAS kuru: <see cref="IsManual"/> true = kullanıcı girişi; false = API/önbellekten yazılan son değer.
/// </summary>
public class GoldRate
{
    public Guid Id { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public decimal HasTryPerGramMid { get; set; }

    public decimal? UsdTryMid { get; set; }

    public bool IsManual { get; set; }

    public DateTime RecordedAtUtc { get; set; }

    public Guid? SetByUserId { get; set; }
}
