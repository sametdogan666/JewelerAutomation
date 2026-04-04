namespace JewelerAutomation.Infrastructure.GoldRates;

public class GoldScraperOptions
{
    public const string SectionName = "GoldScraper";

    public bool Enabled { get; set; } = true;

    public int RequestTimeoutSeconds { get; set; } = 2;

    /// <summary>Denenecek sayfalar (sırayla); regex ile HAS TL/gr yaklaşımı.</summary>
    public List<GoldScraperPage> Pages { get; set; } = new();
}

public class GoldScraperPage
{
    public string Name { get; set; } = "";

    public string Url { get; set; } = "";

    /// <summary>İlk capture grubu fiyat metni (Türkçe: 3.245,67).</summary>
    public string PriceRegex { get; set; } = @"Gram\s*Alt[ıiİI]n[\s\S]{0,400}?(\d{1,2}(?:\.\d{3})*,\d{2})";
}
