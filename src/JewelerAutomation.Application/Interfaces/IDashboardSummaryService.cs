namespace JewelerAutomation.Application.Interfaces;

public interface IDashboardSummaryService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>Boş panel yanıtı — HTTP 500 yerine güvenli varsayılan.</summary>
public static class DashboardSummaryDtoDefaults
{
    public static DashboardSummaryDto Empty { get; } = new(
        NetGoldCapitalHasGram: 0,
        TotalGoldInSafe: 0,
        TotalCashInSafe: 0,
        TotalCustomerGoldDebt: 0,
        TotalCustomerGoldReceivable: 0,
        TotalPersonalGoldDebt: 0,
        TotalPersonalGoldReceivable: 0,
        PhysicalGoldBalance: 0,
        PhysicalCashBalance: 0,
        NetGoldPositionHasGram: 0,
        NetCashPositionTl: 0,
        ExpectedGold: 0,
        GoldGapOrSurplus: 0,
        ProfitHasGram: 0,
        CumulativePeggingProfitHasGram: 0,
        PeggingCount: 0,
        LiveHasTryPerGramMid: null,
        LiveUsdTryMid: null,
        RatesFetchedAtUtc: null,
        RatesAvailable: false,
        RatesFromHistoricalFallback: false,
        RatesFromDefaultFallback: false,
        RatesFromManualOverride: false,
        NetSermayeHasGramAtLivePrice: null,
        NetGoldPositionTlApprox: null);
}

public record DashboardSummaryDto(
    decimal NetGoldCapitalHasGram,
    decimal TotalGoldInSafe,
    decimal TotalCashInSafe,
    decimal TotalCustomerGoldDebt,
    decimal TotalCustomerGoldReceivable,
    decimal TotalPersonalGoldDebt,
    decimal TotalPersonalGoldReceivable,
    decimal PhysicalGoldBalance,
    decimal PhysicalCashBalance,
    decimal NetGoldPositionHasGram,
    decimal NetCashPositionTl,
    decimal ExpectedGold,
    decimal GoldGapOrSurplus,
    decimal ProfitHasGram,
    decimal CumulativePeggingProfitHasGram,
    int PeggingCount,
    decimal? LiveHasTryPerGramMid,
    decimal? LiveUsdTryMid,
    DateTime? RatesFetchedAtUtc,
    bool RatesAvailable,
    /// <summary>Canlı API yokken DailyGoldRates’ten gelen son orta kur kullanıldı.</summary>
    bool RatesFromHistoricalFallback,
    /// <summary>Canlı ve geçmiş yokken yapılandırılmış varsayılan kur kullanıldı.</summary>
    bool RatesFromDefaultFallback,
    /// <summary>Bugün için kullanıcı manuel kur girdi.</summary>
    bool RatesFromManualOverride,
    /// <summary>Kasa + cari has + nakitin canlı has karşılığı (mid).</summary>
    decimal? NetSermayeHasGramAtLivePrice,
    /// <summary>Net altın pozisyonunun yaklaşık TL karşılığı.</summary>
    decimal? NetGoldPositionTlApprox);
