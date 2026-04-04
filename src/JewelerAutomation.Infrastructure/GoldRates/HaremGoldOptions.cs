namespace JewelerAutomation.Infrastructure.GoldRates;

public class HaremGoldOptions
{
    public const string SectionName = "HaremGold";

    /// <summary>Boşsa API çağrısı yapılmaz.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://haremapi.tr/api/v1";

    /// <summary>Harem MADEN sembolü: TL/gram has.</summary>
    public string HasSymbol { get; set; } = "ALTIN";

    public string UsdTrySymbol { get; set; } = "USDTRY";

    /// <summary>Tek istek için üst süre (saniye); aşımda istek iptal (devre kesici).</summary>
    public int RequestTimeoutSeconds { get; set; } = 2;
}
