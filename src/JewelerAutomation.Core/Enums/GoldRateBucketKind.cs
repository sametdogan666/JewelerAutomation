namespace JewelerAutomation.Core.Enums;

public enum GoldRateBucketKind : byte
{
    /// <summary>Saat başına ortalama (30 sn örnekleri).</summary>
    HourlyAverage = 0,

    /// <summary>Gün sonu kapanış (son geçerli kur).</summary>
    DailyClose = 1
}
