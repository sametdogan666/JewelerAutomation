using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.Infrastructure.GoldRates;

/// <summary>
/// Önbellek + Harem (2 sn devre kesici) + isteğe bağlı HTML yedek kaynak.
/// </summary>
public sealed class GoldRateService : IGoldRateService
{
    public const string CacheKey = "GoldRates:Latest";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<HaremGoldOptions> _haremOptions;
    private readonly IOptionsMonitor<GoldScraperOptions> _scraperOptions;
    private readonly IGoldRateCircuitBreaker _circuitBreaker;
    private readonly ILogger<GoldRateService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GoldRateService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsMonitor<HaremGoldOptions> haremOptions,
        IOptionsMonitor<GoldScraperOptions> scraperOptions,
        IGoldRateCircuitBreaker circuitBreaker,
        ILogger<GoldRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _haremOptions = haremOptions;
        _scraperOptions = scraperOptions;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public Task<GoldRatesSnapshot?> GetLatestRatesAsync(CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(CacheKey, out GoldRatesSnapshot? snap);
        return Task.FromResult(snap);
    }

    public async Task<bool> RefreshFromProviderAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GoldRateService: refresh aborted unexpectedly; cache unchanged.");
            return false;
        }
    }

    private async Task<bool> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        GoldRatesSnapshot? snap = null;

        var haremOpt = _haremOptions.CurrentValue;
        var hasApiKey = !string.IsNullOrWhiteSpace(haremOpt.ApiKey);

        if (hasApiKey && !_circuitBreaker.IsHaremOpen)
        {
            snap = await TryFetchHaremAsync(cancellationToken).ConfigureAwait(false);
            if (snap != null)
            {
                _circuitBreaker.RecordHaremSuccess();
                SetCache(snap);
                return true;
            }

            _circuitBreaker.RecordHaremFailure();
            _logger.LogWarning("Harem Altın: kur alınamadı; yedek kaynak veya sonraki döngü denenecek.");
        }
        else if (_circuitBreaker.IsHaremOpen)
            _logger.LogDebug("Harem devre kesici açık; birincil API atlanıyor.");
        else if (!hasApiKey)
            _logger.LogDebug("HaremGold: ApiKey yok; birincil API atlanıyor.");

        var scraperOpt = _scraperOptions.CurrentValue;
        if (scraperOpt.Enabled && scraperOpt.Pages.Count > 0)
        {
            snap = await TryScrapeAsync(cancellationToken).ConfigureAwait(false);
            if (snap != null)
            {
                SetCache(snap);
                return true;
            }
        }

        return false;
    }

    private void SetCache(GoldRatesSnapshot snap)
    {
        _cache.Set(CacheKey, snap, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });
    }

    private async Task<GoldRatesSnapshot?> TryFetchHaremAsync(CancellationToken cancellationToken)
    {
        var opt = _haremOptions.CurrentValue;
        var timeoutSec = Math.Clamp(opt.RequestTimeoutSeconds, 1, 60);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var requestCt = linked.Token;

        var client = _httpClientFactory.CreateClient("HaremAltin");
        using var request = new HttpRequestMessage(HttpMethod.Get, "prices");
        request.Headers.TryAddWithoutValidation("X-API-Key", opt.ApiKey!.Trim());

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCt)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Harem Altın: istek {Timeout}s içinde tamamlanmadı (devre kesici sayılır).", timeoutSec);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Harem Altın: bağlantı/istek hatası.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(requestCt).ConfigureAwait(false);
            }
            catch
            {
                body = "(gövde okunamadı)";
            }

            _logger.LogWarning("Harem Altın API {Status}: {Body}", (int)response.StatusCode, body);
            return null;
        }

        HaremPricesResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<HaremPricesResponse>(JsonOptions, requestCt)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Harem Altın: JSON okuma zaman aşımı ({Timeout}s).", timeoutSec);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Harem Altın yanıtı geçersiz JSON.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Harem Altın yanıtı çözümlenemedi.");
            return null;
        }

        if (payload?.Data == null || payload.Data.Count == 0)
        {
            _logger.LogWarning("Harem Altın boş veya null data döndü.");
            return null;
        }

        var hasSym = opt.HasSymbol.Trim();
        var usdSym = opt.UsdTrySymbol.Trim();
        var hasItem = payload.Data.FirstOrDefault(x =>
            string.Equals(x.Symbol, hasSym, StringComparison.OrdinalIgnoreCase));
        var usdItem = payload.Data.FirstOrDefault(x =>
            string.Equals(x.Symbol, usdSym, StringComparison.OrdinalIgnoreCase));

        if (hasItem == null)
        {
            _logger.LogWarning("Harem fiyat listesinde {Symbol} bulunamadı.", hasSym);
            return null;
        }

        var mid = Math.Round((hasItem.Bid + hasItem.Ask) / 2m, 4);
        decimal? usdBid = usdItem?.Bid;
        decimal? usdAsk = usdItem?.Ask;
        decimal? usdMid = usdItem != null ? Math.Round((usdItem.Bid + usdItem.Ask) / 2m, 4) : null;

        return new GoldRatesSnapshot(
            HasGramTryBid: hasItem.Bid,
            HasGramTryAsk: hasItem.Ask,
            HasGramTryMid: mid,
            UsdTryBid: usdBid,
            UsdTryAsk: usdAsk,
            UsdTryMid: usdMid,
            Source: "HaremAltin",
            FetchedAtUtc: DateTime.UtcNow,
            Stale: payload.Stale);
    }

    private async Task<GoldRatesSnapshot?> TryScrapeAsync(CancellationToken cancellationToken)
    {
        var opt = _scraperOptions.CurrentValue;
        var timeoutSec = Math.Clamp(opt.RequestTimeoutSeconds, 1, 30);
        var client = _httpClientFactory.CreateClient("GoldScraper");

        foreach (var page in opt.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Url))
                continue;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                var ct = linked.Token;

                using var req = new HttpRequestMessage(HttpMethod.Get, page.Url);
                var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Gold scraper {Name}: HTTP {Status}.", page.Name, (int)response.StatusCode);
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var pattern = string.IsNullOrWhiteSpace(page.PriceRegex)
                    ? @"Gram\s*Alt[ıiİI]n[\s\S]{0,400}?(\d{1,2}(?:\.\d{3})*,\d{2})"
                    : page.PriceRegex;
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var m = regex.Match(html);
                if (!m.Success || m.Groups.Count < 2)
                    continue;

                if (!TryParseTurkishDecimal(m.Groups[1].Value, out var mid))
                    continue;

                mid = Math.Round(mid, 4);
                if (mid is < 400m or > 20000m)
                    continue;

                _logger.LogInformation("Gold scraper {Name}: mid={Mid} TRY/g.", page.Name, mid);
                return new GoldRatesSnapshot(
                    HasGramTryBid: mid,
                    HasGramTryAsk: mid,
                    HasGramTryMid: mid,
                    UsdTryBid: null,
                    UsdTryAsk: null,
                    UsdTryMid: null,
                    Source: $"Scrape:{page.Name}",
                    FetchedAtUtc: DateTime.UtcNow,
                    Stale: true);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Gold scraper {Name}: timeout ({Timeout}s).", page.Name, timeoutSec);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Gold scraper {Name} failed.", page.Name);
            }
        }

        _logger.LogWarning("Gold scraper: hiçbir kaynaktan geçerli HAS fiyatı çıkarılamadı.");
        return null;
    }

    private static bool TryParseTurkishDecimal(string raw, out decimal value)
    {
        raw = raw.Trim();
        var normalized = raw.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private sealed class HaremPricesResponse
    {
        public List<HaremPriceItem>? Data { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        public bool Stale { get; set; }
    }

    private sealed class HaremPriceItem
    {
        public string Symbol { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public long Timestamp { get; set; }
    }
}
