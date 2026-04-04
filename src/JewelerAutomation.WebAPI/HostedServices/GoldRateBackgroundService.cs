using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Utilities;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Core.Enums;
using JewelerAutomation.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JewelerAutomation.WebAPI.HostedServices;

/// <summary>
/// ~30 sn’de bir canlı kur çeker, IMemoryCache’i günceller; saatlik ortalama ve gün kapanışını DB’ye yazar.
/// </summary>
public class GoldRateBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GoldRatesHub> _hub;
    private readonly ILogger<GoldRateBackgroundService> _logger;

    private readonly object _sync = new();

    private DateTime _hourBucketUtc;
    private decimal _sumBuy;
    private decimal _sumSell;
    private decimal _sumMid;
    private int _sampleCount;

    private DateTime _utcDateTracked;
    private GoldRatesSnapshot? _lastSnapForDayClose;

    public GoldRateBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<GoldRatesHub> hub,
        ILogger<GoldRateBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
        var now = DateTime.UtcNow;
        _hourBucketUtc = TruncateToHour(now);
        _utcDateTracked = now.Date;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gold = scope.ServiceProvider.GetRequiredService<IGoldRateService>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var goldRatesTable = scope.ServiceProvider.GetRequiredService<IGoldRatesRepository>();

                var todayTr = TurkeyClock.TodayDateOnly();
                var manualLocksDay = await goldRatesTable.HasManualForDateAsync(todayTr, stoppingToken).ConfigureAwait(false);

                var refreshed = false;
                if (!manualLocksDay)
                    refreshed = await gold.RefreshFromProviderAsync(stoppingToken).ConfigureAwait(false);

                var snap = await gold.GetLatestRatesAsync(stoppingToken).ConfigureAwait(false);
                if (snap != null)
                {
                    await ProcessSampleAsync(uow, snap, stoppingToken).ConfigureAwait(false);
                    if (!manualLocksDay)
                    {
                        await goldRatesTable.UpsertAutoAsync(todayTr, snap.HasGramTryMid, snap.UsdTryMid, stoppingToken)
                            .ConfigureAwait(false);
                    }
                }

                if (refreshed)
                {
                    await _hub.Clients.All.SendAsync("RatesUpdated", cancellationToken: stoppingToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Canlı kur arka plan döngüsü hata verdi.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessSampleAsync(IUnitOfWork uow, GoldRatesSnapshot snap, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var currentHour = TruncateToHour(now);
        var today = now.Date;

        (DateTime Day, GoldRatesSnapshot Snap)? dayFlush = null;
        (DateTime HourStart, decimal B, decimal S, decimal M, int C)? hourFlush = null;

        lock (_sync)
        {
            if (today > _utcDateTracked)
            {
                if (_lastSnapForDayClose != null)
                    dayFlush = (_utcDateTracked, _lastSnapForDayClose);
                _utcDateTracked = today;
                _lastSnapForDayClose = snap;
            }
            else
                _lastSnapForDayClose = snap;

            if (currentHour > _hourBucketUtc)
            {
                if (_sampleCount > 0)
                    hourFlush = (_hourBucketUtc, _sumBuy, _sumSell, _sumMid, _sampleCount);

                _hourBucketUtc = currentHour;
                _sumBuy = _sumSell = _sumMid = 0m;
                _sampleCount = 0;
            }

            _sumBuy += snap.HasGramTryBid;
            _sumSell += snap.HasGramTryAsk;
            _sumMid += snap.HasGramTryMid;
            _sampleCount++;
        }

        if (dayFlush.HasValue)
            await PersistDailyCloseAsync(uow, dayFlush.Value.Day, dayFlush.Value.Snap, ct).ConfigureAwait(false);

        if (hourFlush.HasValue)
            await PersistHourlyAverageAsync(uow, hourFlush.Value.HourStart, hourFlush.Value.B, hourFlush.Value.S, hourFlush.Value.M, hourFlush.Value.C, ct)
                .ConfigureAwait(false);
    }

    private static async Task PersistHourlyAverageAsync(
        IUnitOfWork uow,
        DateTime hourStartUtc,
        decimal sumBuy,
        decimal sumSell,
        decimal sumMid,
        int count,
        CancellationToken ct)
    {
        var existing = await uow.DailyGoldRates.GetByBucketAsync(hourStartUtc, GoldRateBucketKind.HourlyAverage, ct)
            .ConfigureAwait(false);
        var avgBuy = Math.Round(sumBuy / count, 4);
        var avgSell = Math.Round(sumSell / count, 4);
        var avgMid = Math.Round(sumMid / count, 4);

        if (existing != null)
        {
            existing.AvgHasTryBuy = avgBuy;
            existing.AvgHasTrySell = avgSell;
            existing.AvgHasTryMid = avgMid;
            existing.SampleCount = count;
            existing.RecordedAtUtc = DateTime.UtcNow;
            uow.DailyGoldRates.Update(existing);
        }
        else
        {
            await uow.DailyGoldRates.AddAsync(new DailyGoldRate
            {
                BucketStartUtc = hourStartUtc,
                Kind = GoldRateBucketKind.HourlyAverage,
                AvgHasTryBuy = avgBuy,
                AvgHasTrySell = avgSell,
                AvgHasTryMid = avgMid,
                SampleCount = count,
                RecordedAtUtc = DateTime.UtcNow
            }, ct).ConfigureAwait(false);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task PersistDailyCloseAsync(
        IUnitOfWork uow,
        DateTime utcDayClosed,
        GoldRatesSnapshot snap,
        CancellationToken ct)
    {
        var bucketStart = new DateTime(utcDayClosed.Year, utcDayClosed.Month, utcDayClosed.Day, 0, 0, 0, DateTimeKind.Utc);
        var existing = await uow.DailyGoldRates.GetByBucketAsync(bucketStart, GoldRateBucketKind.DailyClose, ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            existing.AvgHasTryBuy = snap.HasGramTryBid;
            existing.AvgHasTrySell = snap.HasGramTryAsk;
            existing.AvgHasTryMid = snap.HasGramTryMid;
            existing.ClosingUsdTryMid = snap.UsdTryMid;
            existing.SampleCount = 1;
            existing.RecordedAtUtc = DateTime.UtcNow;
            uow.DailyGoldRates.Update(existing);
        }
        else
        {
            await uow.DailyGoldRates.AddAsync(new DailyGoldRate
            {
                BucketStartUtc = bucketStart,
                Kind = GoldRateBucketKind.DailyClose,
                AvgHasTryBuy = snap.HasGramTryBid,
                AvgHasTrySell = snap.HasGramTryAsk,
                AvgHasTryMid = snap.HasGramTryMid,
                ClosingUsdTryMid = snap.UsdTryMid,
                SampleCount = 1,
                RecordedAtUtc = DateTime.UtcNow
            }, ct).ConfigureAwait(false);
        }

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static DateTime TruncateToHour(DateTime utc)
        => new(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
}
