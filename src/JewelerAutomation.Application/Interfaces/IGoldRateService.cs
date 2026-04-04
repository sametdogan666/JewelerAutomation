namespace JewelerAutomation.Application.Interfaces;

/// <summary>
/// Canlı altın/döviz kuru (önbellek + isteğe bağlı sağlayıcı). Uygulama katmanı altyapıyı bilmez.
/// </summary>
public interface IGoldRateService
{
    /// <summary>IMemoryCache’teki son anlık kurlar; yoksa null.</summary>
    Task<GoldRatesSnapshot?> GetLatestRatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arka planda sağlayıcıları dener (Harem → yedek kaynak), önbelleği günceller.
    /// </summary>
    /// <returns>Önbellek yeni bir anlık kur ile güncellendiyse true.</returns>
    Task<bool> RefreshFromProviderAsync(CancellationToken cancellationToken = default);
}

/// <param name="HasGramTryBid">Harem ALTIN sembolü alış (TL/gram).</param>
/// <param name="HasGramTryAsk">Harem ALTIN sembolü satış (TL/gram).</param>
/// <param name="HasGramTryMid">Ortalama (bid+ask)/2.</param>
public record GoldRatesSnapshot(
    decimal HasGramTryBid,
    decimal HasGramTryAsk,
    decimal HasGramTryMid,
    decimal? UsdTryBid,
    decimal? UsdTryAsk,
    decimal? UsdTryMid,
    string Source,
    DateTime FetchedAtUtc,
    bool Stale);
