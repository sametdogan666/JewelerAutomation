namespace JewelerAutomation.Application.Interfaces;

public interface IDashboardSummaryService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public record PhysicalVaultHistoryPointDto(DateTime At, decimal CumulativeHasGram);

/// <summary>Boş panel yanıtı — HTTP 500 yerine güvenli varsayılan.</summary>
public static class DashboardSummaryDtoDefaults
{
    public static DashboardSummaryDto Empty { get; } = new(
        NetGoldCapitalHasGram: 0,
        TotalGoldInSafe: 0,
        TotalCashInSafe: 0,
        TotalCashInSafeUsd: 0,
        TotalCashInSafeEur: 0,
        TotalCashInSafeGbp: 0,
        TotalCustomerGoldDebt: 0,
        TotalCustomerGoldReceivable: 0,
        TotalPersonalGoldDebt: 0,
        TotalPersonalGoldReceivable: 0,
        SahisGoldLiabilitiesHasGram: 0,
        NetPhysicalEquityHasGram: 0,
        PhysicalGoldBalance: 0,
        PhysicalCashBalance: 0,
        PhysicalCashBalanceUsd: 0,
        PhysicalCashBalanceEur: 0,
        PhysicalCashBalanceGbp: 0,
        NetGoldPositionHasGram: 0,
        NetCashPositionTl: 0,
        NetCashPositionUsd: 0,
        NetCashPositionEur: 0,
        NetCashPositionGbp: 0,
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
        NetGoldPositionTlApprox: null,
        PhysicalVaultHistory: []);
}

public record DashboardSummaryDto(
    decimal NetGoldCapitalHasGram,
    decimal TotalGoldInSafe,
    /// <summary>Defter nakit bakiyesi (TL).</summary>
    decimal TotalCashInSafe,
    decimal TotalCashInSafeUsd,
    decimal TotalCashInSafeEur,
    decimal TotalCashInSafeGbp,
    decimal TotalCustomerGoldDebt,
    decimal TotalCustomerGoldReceivable,
    decimal TotalPersonalGoldDebt,
    decimal TotalPersonalGoldReceivable,
    /// <summary>Şahıslara göre fiziki altın yükümlülüğü (pozitif has bakiyesi toplamı).</summary>
    decimal SahisGoldLiabilitiesHasGram,
    /// <summary>Fiziki altın − şahıs emanet yükümlülükleri (Has).</summary>
    decimal NetPhysicalEquityHasGram,
    decimal PhysicalGoldBalance,
    decimal PhysicalCashBalance,
    decimal PhysicalCashBalanceUsd,
    decimal PhysicalCashBalanceEur,
    decimal PhysicalCashBalanceGbp,
    decimal NetGoldPositionHasGram,
    decimal NetCashPositionTl,
    decimal NetCashPositionUsd,
    decimal NetCashPositionEur,
    decimal NetCashPositionGbp,
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
    decimal? NetGoldPositionTlApprox,
    /// <summary>Kasa hareketleri kronolojik kümülatif fiziki has (grafik).</summary>
    IReadOnlyList<PhysicalVaultHistoryPointDto> PhysicalVaultHistory);
