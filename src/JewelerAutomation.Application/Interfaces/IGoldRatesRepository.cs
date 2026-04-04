namespace JewelerAutomation.Application.Interfaces;

public interface IGoldRatesRepository
{
    Task<GoldRateRow?> GetByEffectiveDateAsync(DateOnly effectiveDate, bool isManual, CancellationToken cancellationToken = default);

    Task<bool> HasManualForDateAsync(DateOnly effectiveDate, CancellationToken cancellationToken = default);

    Task UpsertManualAsync(
        DateOnly effectiveDate,
        decimal hasTryPerGramMid,
        decimal? usdTryMid,
        Guid? setByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Otomatik (API) kur — bugün için tek satır, IsManual=false.</summary>
    Task UpsertAutoAsync(
        DateOnly effectiveDate,
        decimal hasTryPerGramMid,
        decimal? usdTryMid,
        CancellationToken cancellationToken = default);
}

public sealed record GoldRateRow(
    decimal HasTryPerGramMid,
    decimal? UsdTryMid,
    DateTime RecordedAtUtc,
    bool IsManual);
